namespace ProjectPricing.Dominio.Materiais;

public interface IMaterialRepository
{
    Task<Material?> ObterPorIdAsync(string id, CancellationToken cancellationToken);

    Task InserirAsync(Material material, CancellationToken cancellationToken);

    Task AtualizarAsync(Material material, CancellationToken cancellationToken);
}
