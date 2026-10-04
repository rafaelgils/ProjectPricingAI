namespace ProjectPricing.Dominio.Materiais;

/// <summary>Filtros da listagem do catálogo. Busca e categoria ignoram maiúsculas e acentos.</summary>
/// <param name="Busca">Trecho do nome ou de um sinônimo.</param>
/// <param name="Categoria">Categoria exata.</param>
/// <param name="Status">Nulo traz ativos e inativos.</param>
/// <param name="Pagina">Começa em 1.</param>
public sealed record FiltroMateriais(
    string? Busca,
    string? Categoria,
    StatusMaterial? Status,
    int Pagina,
    int Tamanho);

public sealed record PaginaDeMateriais(IReadOnlyList<Material> Itens, long Total);
