using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Api.Materiais;

/// <summary>Material como a API devolve (standards.md §5: moeda explícita em toda resposta com valor).</summary>
public sealed record MaterialResposta(
    string Id,
    string Nome,
    IReadOnlyList<string> Sinonimos,
    TipoMaterial Tipo,
    string Categoria,
    UnidadeMedida Unidade,
    decimal PrecoUnitario,
    string Moeda,
    string Fornecedor,
    StatusMaterial Status,
    DateTimeOffset AtualizadoEm)
{
    public const string MoedaPadrao = "BRL";

    public static MaterialResposta De(Material material) => new(
        material.Id ?? throw new InvalidOperationException("Material sem id não pode ser devolvido."),
        material.Nome,
        material.Sinonimos,
        material.Tipo,
        material.Categoria,
        material.Unidade,
        material.PrecoUnitario,
        MoedaPadrao,
        material.Fornecedor,
        material.Status,
        material.AtualizadoEm);
}

/// <summary>Página de resultados (standards.md §5: parâmetros pagina e tamanho; resposta com total).</summary>
public sealed record PaginaResposta<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, long Total);
