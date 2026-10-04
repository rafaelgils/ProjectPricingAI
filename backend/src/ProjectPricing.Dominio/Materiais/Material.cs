using ProjectPricing.Dominio.Comum;

namespace ProjectPricing.Dominio.Materiais;

/// <summary>Insumo ou serviço do catálogo (business-rules.md RN10).</summary>
public sealed class Material
{
    public Material(
        string nome,
        IEnumerable<string> sinonimos,
        TipoMaterial tipo,
        string categoria,
        UnidadeMedida unidade,
        decimal precoUnitario,
        string fornecedor,
        DateTimeOffset atualizadoEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoria);
        ArgumentException.ThrowIfNullOrWhiteSpace(fornecedor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(precoUnitario);

        Nome = nome.Trim();
        Sinonimos = [.. sinonimos.Select(s => s.Trim())];
        Tipo = tipo;
        Categoria = categoria.Trim();
        Unidade = unidade;
        PrecoUnitario = precoUnitario;
        Fornecedor = fornecedor.Trim();
        Status = StatusMaterial.Ativo;
        AtualizadoEm = atualizadoEm;
    }

    // Usado pelo driver do MongoDB na leitura.
    private Material()
    {
        Nome = string.Empty;
        Categoria = string.Empty;
        Fornecedor = string.Empty;
    }

    public string? Id { get; private set; }

    public string Nome { get; private set; }

    public IReadOnlyList<string> Sinonimos { get; private set; } = [];

    public TipoMaterial Tipo { get; private set; }

    public string Categoria { get; private set; }

    public UnidadeMedida Unidade { get; private set; }

    public decimal PrecoUnitario { get; private set; }

    public string Fornecedor { get; private set; }

    public StatusMaterial Status { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }
}
