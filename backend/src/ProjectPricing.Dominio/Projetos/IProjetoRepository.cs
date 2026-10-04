namespace ProjectPricing.Dominio.Projetos;

public interface IProjetoRepository
{
    Task<Projeto?> ObterPorIdAsync(string id, CancellationToken cancellationToken);

    Task InserirAsync(Projeto projeto, CancellationToken cancellationToken);

    Task AtualizarAsync(Projeto projeto, CancellationToken cancellationToken);
}
