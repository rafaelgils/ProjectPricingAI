using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Dominio.Precificacao;

/// <summary>Item a precificar: o material do catálogo (preço já congelado, RN02) e a medida pedida.</summary>
public sealed record ItemACalcular(
    string MaterialId,
    string Nome,
    UnidadeMedida UnidadeMaterial,
    decimal PrecoUnitario,
    MedidaInformada Medida);

/// <summary>Item calculado. Todos os valores com 2 casas, como são gravados e exibidos (RN08).</summary>
public sealed record ItemCalculado(
    string MaterialId,
    string Nome,
    decimal Quantidade,
    UnidadeMedida Unidade,
    decimal PrecoUnitario,
    decimal Subtotal);

public sealed record ResultadoCalculo(IReadOnlyList<ItemCalculado> Itens, decimal Total);

public interface IServicoPrecificacao
{
    ResultadoCalculo Calcular(IReadOnlyList<ItemACalcular> itens);

    ItemCalculado CalcularItem(ItemACalcular item);

    /// <summary>Total = soma dos subtotais arredondados (RN08). No refinamento, mistura itens novos e mantidos (RN02).</summary>
    decimal Totalizar(IEnumerable<decimal> subtotais);
}

/// <summary>
/// Cálculo determinístico do valor (ADR-001, RN01): quantidade × preço unitário, sem margem e sem impostos.
/// O LLM nunca calcula; ele só informa as medidas.
/// </summary>
public sealed class ServicoPrecificacao(IConversorUnidades conversor) : IServicoPrecificacao
{
    public ResultadoCalculo Calcular(IReadOnlyList<ItemACalcular> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);

        var calculados = itens.Select(CalcularItem).ToList();
        return new ResultadoCalculo(calculados, Totalizar(calculados.Select(i => i.Subtotal)));
    }

    public ItemCalculado CalcularItem(ItemACalcular item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(item.PrecoUnitario);

        // A quantidade entra no cálculo já com 2 casas, igual à exibida: o cliente refaz a conta (RN08).
        var quantidade = ArredondamentoMonetario.ParaGravacao(conversor.Converter(item.Medida, item.UnidadeMaterial));
        if (quantidade <= 0)
        {
            throw new MedidaInvalidaException(
                $"A quantidade de \"{item.Nome}\" fica zero ao arredondar para 2 casas; informe uma medida maior.");
        }

        var subtotal = ArredondamentoMonetario.ParaGravacao(quantidade * item.PrecoUnitario);
        return new ItemCalculado(item.MaterialId, item.Nome, quantidade, item.UnidadeMaterial, item.PrecoUnitario, subtotal);
    }

    public decimal Totalizar(IEnumerable<decimal> subtotais) => ArredondamentoMonetario.Somar(subtotais);
}
