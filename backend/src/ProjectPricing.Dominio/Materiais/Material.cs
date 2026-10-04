using ProjectPricing.Dominio.Comum;

namespace ProjectPricing.Dominio.Materiais;

/// <summary>Insumo ou serviço do catálogo (business-rules.md RN04 e RN10).</summary>
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
        Nome = string.Empty;
        Categoria = string.Empty;
        Fornecedor = string.Empty;
        Definir(nome, sinonimos, tipo, categoria, unidade, precoUnitario, fornecedor, atualizadoEm);
        Status = StatusMaterial.Ativo;
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

    /// <summary>Nome e sinônimos: os termos que não podem se repetir no catálogo (RN10).</summary>
    public IEnumerable<string> NomeESinonimos => Sinonimos.Prepend(Nome);

    /// <summary>
    /// Altera os dados do catálogo. Cotações já emitidas guardam cópia do preço e não mudam (RN02).
    /// </summary>
    public void Alterar(
        string nome,
        IEnumerable<string> sinonimos,
        TipoMaterial tipo,
        string categoria,
        UnidadeMedida unidade,
        decimal precoUnitario,
        string fornecedor,
        DateTimeOffset alteradoEm)
    {
        Definir(nome, sinonimos, tipo, categoria, unidade, precoUnitario, fornecedor, alteradoEm);
    }

    /// <summary>Exclusão lógica: o material sai do catálogo, mas continua no histórico (RN04).</summary>
    public void Inativar(DateTimeOffset alteradoEm)
    {
        Status = StatusMaterial.Inativo;
        AtualizadoEm = alteradoEm;
    }

    public void Reativar(DateTimeOffset alteradoEm)
    {
        Status = StatusMaterial.Ativo;
        AtualizadoEm = alteradoEm;
    }

    private void Definir(
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
        ArgumentNullException.ThrowIfNull(sinonimos);
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
        AtualizadoEm = atualizadoEm;
    }
}
