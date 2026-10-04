namespace ProjectPricing.Dominio.Materiais;

public interface IMaterialRepository
{
    Task<Material?> ObterPorIdAsync(string id, CancellationToken cancellationToken);

    Task<PaginaDeMateriais> ListarAsync(FiltroMateriais filtro, CancellationToken cancellationToken);

    /// <summary>Todo o catálogo ativo, para a similaridade em memória da RN09 (plano, P9).</summary>
    Task<IReadOnlyList<Material>> ListarAtivosAsync(CancellationToken cancellationToken);

    /// <summary>Materiais pelos ids, ativos ou não (itens já cotados podem ter material inativado).</summary>
    Task<IReadOnlyList<Material>> ObterPorIdsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Outro material (ativo ou inativo) cujo nome ou sinônimo coincide com algum dos termos,
    /// sem diferenciar maiúsculas e acentos (RN10). Nulo se não houver.
    /// </summary>
    Task<Material?> BuscarConflitoDeNomeAsync(
        IReadOnlyCollection<string> termos,
        string? ignorarId,
        CancellationToken cancellationToken);

    /// <exception cref="Excecoes.MaterialDuplicadoException">Nome ou sinônimo repetido detectado pelo índice único.</exception>
    Task InserirAsync(Material material, CancellationToken cancellationToken);

    /// <exception cref="Excecoes.MaterialDuplicadoException">Nome ou sinônimo repetido detectado pelo índice único.</exception>
    Task AtualizarAsync(Material material, CancellationToken cancellationToken);
}
