using MongoDB.Bson;
using MongoDB.Driver;
using ProjectPricing.Aplicacao.Idempotencia;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Tenancy;

namespace ProjectPricing.Infraestrutura.Repositorios;

/// <summary>
/// Armazém de chaves de idempotência no MongoDB (ADR-012). O índice único (tenantId, usuarioId, chave) decide,
/// de forma atômica, qual das chamadas simultâneas processa a requisição. Um índice TTL descarta as chaves antigas.
/// </summary>
public sealed class ArmazemIdempotenciaMongo(
    IMongoCollection<BsonDocument> colecao,
    ITenantAtual tenant,
    IUsuarioAtual usuario,
    TimeProvider relogio) : IArmazemIdempotencia
{
    public async Task<ResultadoReserva> ReservarAsync(string chave, string impressaoDigital, CancellationToken cancellationToken)
    {
        var documento = new BsonDocument
        {
            ["tenantId"] = tenant.TenantId,
            ["usuarioId"] = usuario.KeycloakId,
            ["chave"] = chave,
            ["impressaoDigital"] = impressaoDigital,
            ["concluida"] = false,
            ["criadoEm"] = relogio.GetUtcNow().UtcDateTime,
        };

        try
        {
            await colecao.InsertOneAsync(documento, options: null, cancellationToken);
            return new ResultadoReserva(SituacaoReserva.Nova);
        }
        catch (MongoWriteException erro) when (erro.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await AvaliarExistenteAsync(chave, impressaoDigital, cancellationToken);
        }
    }

    public Task ConcluirAsync(string chave, RespostaGuardada resposta, CancellationToken cancellationToken)
    {
        var atualizacao = Builders<BsonDocument>.Update
            .Set("concluida", true)
            .Set("statusHttp", resposta.StatusHttp)
            .Set("location", resposta.Location is null ? BsonNull.Value : (BsonValue)new BsonString(resposta.Location))
            .Set("corpo", resposta.Corpo);
        return colecao.UpdateOneAsync(DaChave(chave), atualizacao, cancellationToken: cancellationToken);
    }

    public Task LiberarAsync(string chave, CancellationToken cancellationToken) =>
        colecao.DeleteOneAsync(DaChave(chave), cancellationToken);

    private async Task<ResultadoReserva> AvaliarExistenteAsync(string chave, string impressaoDigital, CancellationToken cancellationToken)
    {
        using var cursor = await colecao.FindAsync(DaChave(chave), cancellationToken: cancellationToken);
        var existente = await cursor.FirstOrDefaultAsync(cancellationToken);

        if (existente is null)
        {
            // A chave foi liberada entre a inserção e a leitura: quem chegou agora deve tentar de novo.
            return await ReservarAsync(chave, impressaoDigital, cancellationToken);
        }

        if (existente["impressaoDigital"].AsString != impressaoDigital)
        {
            return new ResultadoReserva(SituacaoReserva.Conflito);
        }

        if (!existente["concluida"].AsBoolean)
        {
            return new ResultadoReserva(SituacaoReserva.EmAndamento);
        }

        var location = existente.GetValue("location", BsonNull.Value);
        return new ResultadoReserva(
            SituacaoReserva.Repetida,
            new RespostaGuardada(existente["statusHttp"].AsInt32, location.IsBsonNull ? null : location.AsString, existente["corpo"].AsString));
    }

    private FilterDefinition<BsonDocument> DaChave(string chave) =>
        Builders<BsonDocument>.Filter.Eq("tenantId", tenant.TenantId)
        & Builders<BsonDocument>.Filter.Eq("usuarioId", usuario.KeycloakId)
        & Builders<BsonDocument>.Filter.Eq("chave", chave);
}
