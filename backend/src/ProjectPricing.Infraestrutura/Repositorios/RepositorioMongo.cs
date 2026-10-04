using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ProjectPricing.Infraestrutura.Repositorios;

/// <summary>Inserção, substituição e busca por id, comuns às coleções do sistema.</summary>
public abstract class RepositorioMongo<TEntidade>
    where TEntidade : class
{
    private readonly Expression<Func<TEntidade, string?>> _seletorId;
    private readonly Func<TEntidade, string?> _obterId;

    protected RepositorioMongo(IMongoCollection<TEntidade> colecao, Expression<Func<TEntidade, string?>> seletorId)
    {
        Colecao = colecao;
        _seletorId = seletorId;
        _obterId = seletorId.Compile();
    }

    protected IMongoCollection<TEntidade> Colecao { get; }

    public virtual Task InserirAsync(TEntidade entidade, CancellationToken cancellationToken)
    {
        return Colecao.InsertOneAsync(entidade, options: null, cancellationToken);
    }

    public virtual Task AtualizarAsync(TEntidade entidade, CancellationToken cancellationToken)
    {
        var id = _obterId(entidade)
            ?? throw new InvalidOperationException($"{typeof(TEntidade).Name} sem id não pode ser atualizado.");

        return Colecao.ReplaceOneAsync(FiltroPorCampo(_seletorId, id), entidade, cancellationToken: cancellationToken);
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

        using var cursor = await Colecao.FindAsync(FiltroPorCampo(campo, valor), cancellationToken: cancellationToken);
        return await cursor.FirstOrDefaultAsync(cancellationToken);
    }

    private static FilterDefinition<TEntidade> FiltroPorCampo(Expression<Func<TEntidade, string?>> campo, string valor)
    {
        return Builders<TEntidade>.Filter.Eq(campo, valor);
    }
}
