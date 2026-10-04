using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Dominio.Precificacao;

/// <summary>Converte a medida informada para uma unidade do catálogo (RN05). Uma regra por unidade de destino.</summary>
public interface IRegraConversao
{
    UnidadeMedida Destino { get; }

    /// <summary>Quantidade na unidade de destino, ainda sem arredondamento.</summary>
    /// <exception cref="MedidaInvalidaException">Unidade incompatível, medida faltando ou não positiva.</exception>
    decimal Converter(MedidaInformada medida);
}

/// <summary>Regra para unidades em que a conversão é só multiplicar por um fator fixo.</summary>
public abstract class RegraPorFator(UnidadeMedida destino, IReadOnlyDictionary<string, decimal> fatores) : IRegraConversao
{
    public UnidadeMedida Destino { get; } = destino;

    public decimal Converter(MedidaInformada medida)
    {
        if (medida.TemDimensoes)
        {
            throw new MedidaInvalidaException(
                $"Largura e altura só valem para materiais em m²; o material está em {UnidadesInformadas.Codigo(Destino)}.");
        }

        var quantidade = UnidadesInformadas.QuantidadePositiva(medida.Quantidade);
        return quantidade * UnidadesInformadas.Fator(fatores, medida.Unidade, Destino);
    }
}

public sealed class RegraMetro() : RegraPorFator(UnidadeMedida.Metro, UnidadesInformadas.Lineares);

public sealed class RegraLitro() : RegraPorFator(UnidadeMedida.Litro, new Dictionary<string, decimal>
{
    ["ml"] = 0.001m,
    ["l"] = 1m,
});

public sealed class RegraHora() : RegraPorFator(UnidadeMedida.Hora, new Dictionary<string, decimal>
{
    ["min"] = 1m / 60m,
    ["h"] = 1m,
});

public sealed class RegraUnidade() : RegraPorFator(UnidadeMedida.Unidade, new Dictionary<string, decimal>
{
    ["un"] = 1m,
});

/// <summary>
/// m²: aceita área (mm², cm², m²) ou largura × altura numa unidade linear.
/// Com dimensões, a quantidade, se informada, é o número de peças.
/// </summary>
public sealed class RegraMetroQuadrado : IRegraConversao
{
    private static readonly Dictionary<string, decimal> FatoresArea = new()
    {
        ["mm2"] = 0.000001m,
        ["cm2"] = 0.0001m,
        ["m2"] = 1m,
    };

    public UnidadeMedida Destino => UnidadeMedida.MetroQuadrado;

    public decimal Converter(MedidaInformada medida)
    {
        if (!medida.TemDimensoes)
        {
            var area = UnidadesInformadas.QuantidadePositiva(medida.Quantidade);
            return area * UnidadesInformadas.Fator(FatoresArea, medida.Unidade, Destino);
        }

        if (medida.Largura is not { } largura || medida.Altura is not { } altura || largura <= 0 || altura <= 0)
        {
            throw new MedidaInvalidaException("Informe largura e altura maiores que zero.");
        }

        var fatorLinear = UnidadesInformadas.Fator(UnidadesInformadas.Lineares, medida.Unidade, Destino);
        var pecas = medida.Quantidade is null ? 1m : UnidadesInformadas.QuantidadePositiva(medida.Quantidade);
        return largura * fatorLinear * (altura * fatorLinear) * pecas;
    }
}

/// <summary>Códigos de unidade aceitos na descrição (tabela da RN05) e validações comuns.</summary>
internal static class UnidadesInformadas
{
    public static readonly IReadOnlyDictionary<string, decimal> Lineares = new Dictionary<string, decimal>
    {
        ["mm"] = 0.001m,
        ["cm"] = 0.01m,
        ["m"] = 1m,
    };

    public static string Codigo(UnidadeMedida unidade) => CodigosEnum<UnidadeMedida>.Codigo(unidade);

    public static decimal QuantidadePositiva(decimal? quantidade)
    {
        return quantidade is > 0
            ? quantidade.Value
            : throw new MedidaInvalidaException("Informe uma quantidade maior que zero.");
    }

    public static decimal Fator(IReadOnlyDictionary<string, decimal> fatores, string unidadeInformada, UnidadeMedida destino)
    {
        var codigo = Normalizar(unidadeInformada);
        return fatores.TryGetValue(codigo, out var fator)
            ? fator
            : throw new MedidaInvalidaException(
                $"A unidade \"{unidadeInformada}\" não pode ser convertida para {Codigo(destino)}.");
    }

    /// <summary>"cm²" e "CM2" viram "cm2"; "L" vira "l".</summary>
    private static string Normalizar(string? unidade) =>
        (unidade ?? string.Empty).Trim().ToLowerInvariant().Replace('²', '2');
}
