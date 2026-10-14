using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using ProjectPricing.Dominio.Tenancy;

namespace ProjectPricing.Infraestrutura.Repositorios;

/// <summary>
/// Inserção, substituição e busca por id, comuns às coleções do sistema. Toda operação é restrita ao tenant
/// da requisição (ADR-011): o isolamento fica aqui, num único ponto, e não depende de cada caso de uso lembrar dele.
/// </summary>
public abstract class RepositorioMongo<TEntidade>
    where TEntidade : class, IPertenceAoTenant
{
    /// <summary>Nome do campo no documento (convenção camelCase).</summary>
    public const string CampoTenant = "tenantId";

    private readonly Expression<Func<TEntidade, string?>> _seletorId;
    private readonly Func<TEntidade, string?> _obterId;
    private readonly ITenantAtual _tenant;

    protected RepositorioMongo(
        IMongoCollection<TEntidade> colecao,
        Expression<Func<TEntidade, string?>> seletorId,
        ITenantAtual tenant)
    {
        Colecao = colecao;
        _seletorId = seletorId;
        _obterId = seletorId.Compile();
        _tenant = tenant;
    }

    protected IMongoCollection<TEntidade> Colecao { get; }

    /// <summary>Filtro do tenant atual; todas as consultas e escritas o combinam com as próprias condições.</summary>
    protected FilterDefinition<TEntidade> DoTenant =>
        Builders<TEntidade>.Filter.Eq(CampoTenant, _tenant.TenantId);

    public virtual Task InserirAsync(TEntidade entidade, CancellationToken cancellationToken)
    {
        entidade.DefinirTenant(_tenant.TenantId);
        return Colecao.InsertOneAsync(entidade, options: null, cancellationToken);
    }

    public virtual Task AtualizarAsync(TEntidade entidade, CancellationToken cancellationToken)
    {
        var id = _obterId(entidade)
            ?? throw new InvalidOperationException($"{typeof(TEntidade).Name} sem id não pode ser atualizado.");

        return Colecao.ReplaceOneAsync(
            FiltroPorCampo(_seletorId, id) & DoTenant, entidade, cancellationToken: cancellationToken);
    }

    public Task<TEntidade?> ObterPorIdAsync(string id, CancellationToken cancellationToken)
    {
        return ObterPorCampoObjectIdAsync(_seletorId, id, cancellationToken);
    }

    /// <summary>Busca por um campo gravado como ObjectId. Um id em formato inválido não existe: retorna nulo.</summary>
    protected async Task<TEntidade?> ObterPorCampoObjectIdAsync(
        Expression<Func<TEntidade, string?>> campo,
        string valor,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(valor, out _))
        {
            return null;
        }

        using var cursor = await Colecao.FindAsync(
            FiltroPorCampo(campo, valor) & DoTenant, cancellationToken: cancellationToken);
        return await cursor.FirstOrDefaultAsync(cancellationToken);
    }

    private static FilterDefinition<TEntidade> FiltroPorCampo(Expression<Func<TEntidade, string?>> campo, string valor)
    {
        return Builders<TEntidade>.Filter.Eq(campo, valor);
    }
}
