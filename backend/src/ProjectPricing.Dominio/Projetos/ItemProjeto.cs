using ProjectPricing.Dominio.Comum;

namespace ProjectPricing.Dominio.Projetos;

/// <summary>
/// Material usado no projeto. Nome e preço unitário são cópias congeladas do catálogo (RN02, ADR-007).
/// </summary>
public sealed class ItemProjeto
{
    public ItemProjeto(
        string materialId,
        string nomeSnapshot,
        decimal quantidade,
        UnidadeMedida unidade,
        decimal precoUnitarioSnapshot,
        decimal subtotal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeSnapshot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantidade);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(precoUnitarioSnapshot);
        ArgumentOutOfRangeException.ThrowIfNegative(subtotal);

        MaterialId = materialId;
        NomeSnapshot = nomeSnapshot;
        Quantidade = quantidade;
        Unidade = unidade;
        PrecoUnitarioSnapshot = precoUnitarioSnapshot;
        Subtotal = subtotal;
    }

    // Usado pelo driver do MongoDB na leitura.
    private ItemProjeto()
    {
        MaterialId = string.Empty;
        NomeSnapshot = string.Empty;
    }

    public string MaterialId { get; private set; }

    public string NomeSnapshot { get; private set; }

    public decimal Quantidade { get; private set; }

    public UnidadeMedida Unidade { get; private set; }

    public decimal PrecoUnitarioSnapshot { get; private set; }

    public decimal Subtotal { get; private set; }
}
