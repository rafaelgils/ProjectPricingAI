using MongoDB.Driver;
using ProjectPricing.Dominio.Conversas;

namespace ProjectPricing.Infraestrutura.Repositorios;

public sealed class ConversaRepository(IMongoCollection<Conversa> colecao)
    : RepositorioMongo<Conversa>(colecao, c => c.Id), IConversaRepository
{
    public Task<Conversa?> ObterPorProjetoIdAsync(string projetoId, CancellationToken cancellationToken)
    {
        return ObterPorCampoObjectIdAsync(c => c.ProjetoId, projetoId, cancellationToken);
    }
}
