using MongoDB.Driver;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Infraestrutura.Repositorios;

public sealed class ProjetoRepository(IMongoCollection<Projeto> colecao)
    : RepositorioMongo<Projeto>(colecao, p => p.Id), IProjetoRepository
{
    private static readonly FilterDefinitionBuilder<Projeto> Filtro = Builders<Projeto>.Filter;

    public async Task<PaginaDeProjetos> ListarAsync(FiltroProjetos filtro, CancellationToken cancellationToken)
    {
        var condicao = MontarFiltro(filtro);

        var total = await Colecao.CountDocumentsAsync(condicao, cancellationToken: cancellationToken);
        var opcoes = new FindOptions<Projeto>
        {
            Sort = Builders<Projeto>.Sort.Descending(p => p.AlteradoEm),
            Skip = (filtro.Pagina - 1) * filtro.Tamanho,
            Limit = filtro.Tamanho,
        };
        using var cursor = await Colecao.FindAsync(condicao, opcoes, cancellationToken);
        return new PaginaDeProjetos(await cursor.ToListAsync(cancellationToken), total);
    }

    /// <summary>
    /// Concorrência otimista: substitui só se a versão no banco for a lida. Se outra gravação venceu,
    /// nada é sobrescrito.
    /// </summary>
    public override async Task AtualizarAsync(Projeto projeto, CancellationToken cancellationToken)
    {
        var id = projeto.Id ?? throw new InvalidOperationException("Projeto sem id não pode ser atualizado.");
        var versaoLida = projeto.Versao;
        var condicao = Filtro.Eq(p => p.Id, id) & Filtro.Eq(p => p.Versao, versaoLida);

        projeto.AvancarVersao();
        var resultado = await Colecao.ReplaceOneAsync(condicao, projeto, cancellationToken: cancellationToken);
        if (resultado.IsAcknowledged && resultado.MatchedCount == 0)
        {
            throw new ConflitoDeEdicaoException();
        }
    }

    public static FilterDefinition<Projeto> MontarFiltro(FiltroProjetos filtro)
    {
        var condicao = Filtro.Empty;
        if (filtro.ClienteId is not null)
        {
            condicao &= Filtro.Eq(p => p.ClienteId, filtro.ClienteId);
        }

        if (filtro.Status is { } status)
        {
            condicao &= Filtro.Eq(p => p.Status, status);
        }

        return condicao;
    }
}
