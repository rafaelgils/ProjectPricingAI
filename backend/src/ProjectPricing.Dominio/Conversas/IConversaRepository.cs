namespace ProjectPricing.Dominio.Conversas;

public interface IConversaRepository
{
    Task<Conversa?> ObterPorProjetoIdAsync(string projetoId, CancellationToken cancellationToken);

    Task InserirAsync(Conversa conversa, CancellationToken cancellationToken);

    Task AtualizarAsync(Conversa conversa, CancellationToken cancellationToken);
}
