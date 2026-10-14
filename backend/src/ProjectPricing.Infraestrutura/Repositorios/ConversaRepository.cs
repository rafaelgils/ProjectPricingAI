using MongoDB.Driver;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Tenancy;

namespace ProjectPricing.Infraestrutura.Repositorios;

public sealed class ConversaRepository(IMongoCollection<Conversa> colecao, ITenantAtual tenant)
    : RepositorioMongo<Conversa>(colecao, c => c.Id, tenant), IConversaRepository
{
    public Task<Conversa?> ObterPorProjetoIdAsync(string projetoId, CancellationToken cancellationToken)
    {
        return ObterPorCampoObjectIdAsync(c => c.ProjetoId, projetoId, cancellationToken);
    }
}
