using System.ComponentModel;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Precificacao;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Cotacao;

/// <summary>Resultado de buscarMateriais para um termo, no formato das tools (MCP e LLM).</summary>
public sealed record ResultadoBuscaDto(
    string Termo,
    ClassificacaoTermo Classificacao,
    decimal Similaridade,
    string? MaterialId,
    string? Nome,
    string? Unidade)
{
    public static ResultadoBuscaDto De(ResultadoTermo resultado) => new(
        resultado.Termo,
        resultado.Classificacao,
        resultado.Similaridade,
        resultado.Material?.Id,
        resultado.Material?.Nome,
        resultado.Material is null ? null : CodigosEnum<UnidadeMedida>.Codigo(resultado.Material.Unidade));
}

/// <summary>Item pedido nas tools calcular e salvarProjeto.</summary>
public sealed record ItemPedidoDto(
    [property: Description("Id do material no catálogo.")] string MaterialId,
    [property: Description("Quantidade na unidade informada; com largura e altura, é o número de peças.")] decimal? Quantidade,
    [property: Description("Unidade informada: mm, cm, m, mm2, cm2, m2, ml, L, min, h ou un.")] string Unidade,
    [property: Description("Largura, para itens vendidos por área.")] decimal? Largura,
    [property: Description("Altura, para itens vendidos por área.")] decimal? Altura)
{
    public ItemPedido ParaItemPedido() => new(MaterialId, new MedidaInformada(Quantidade, Unidade, Largura, Altura));
}

public sealed record ItemCalculadoDto(
    string MaterialId,
    string Nome,
    decimal Quantidade,
    string Unidade,
    decimal PrecoUnitario,
    decimal Subtotal);

public sealed record CalculoDto(IReadOnlyList<ItemCalculadoDto> Itens, decimal ValorTotal, string Moeda)
{
    public const string MoedaPadrao = "BRL";

    public static CalculoDto De(ResultadoCalculo resultado) => new(
        [.. resultado.Itens.Select(i => new ItemCalculadoDto(
            i.MaterialId, i.Nome, i.Quantidade, CodigosEnum<UnidadeMedida>.Codigo(i.Unidade), i.PrecoUnitario, i.Subtotal))],
        resultado.Total,
        MoedaPadrao);

    /// <summary>Cotação gravada no projeto, com os valores congelados (ADR-007).</summary>
    public static CalculoDto De(Projeto projeto) => new(
        [.. projeto.Itens.Select(i => new ItemCalculadoDto(
            i.MaterialId, i.NomeSnapshot, i.Quantidade, CodigosEnum<UnidadeMedida>.Codigo(i.Unidade), i.PrecoUnitarioSnapshot, i.Subtotal))],
        projeto.ValorTotal ?? 0.00m,
        MoedaPadrao);
}
