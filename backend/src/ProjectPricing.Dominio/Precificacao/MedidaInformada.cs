namespace ProjectPricing.Dominio.Precificacao;

/// <summary>
/// Medida como o LLM a extrai da descrição (ADR-001, RN05). Duas formas:
/// <list type="bullet">
/// <item>Quantidade e unidade: "500 ml", "90 min", "3 un", "0,36 m²".</item>
/// <item>Largura e altura numa unidade linear, para materiais em m²: "60 x 60 cm". A
/// <see cref="Quantidade"/>, se houver, é o número de peças ("2 placas de 60 x 60 cm").</item>
/// </list>
/// </summary>
public sealed record MedidaInformada(decimal? Quantidade, string Unidade, decimal? Largura = null, decimal? Altura = null)
{
    public bool TemDimensoes => Largura is not null || Altura is not null;
}
