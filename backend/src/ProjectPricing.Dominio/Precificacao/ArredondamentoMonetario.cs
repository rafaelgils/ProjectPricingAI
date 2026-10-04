namespace ProjectPricing.Dominio.Precificacao;

/// <summary>
/// Precisão e arredondamento da RN08 (business-rules.md). Único lugar do sistema que arredonda valores.
/// </summary>
public static class ArredondamentoMonetario
{
    /// <summary>A 3ª casa decimal a partir da qual o valor sobe (6 a 9 sobe; 5 ou menos desce).</summary>
    public const int LimiarArredondamento = 6;

    public const int CasasCalculo = 3;
    public const int CasasGravacao = 2;

    /// <summary>Soma neutra que fixa a escala em 2 casas (43,2 vira 43,20), para banco e JSON mostrarem os centavos.</summary>
    private const decimal EscalaDuasCasas = 0.00m;

    /// <summary>
    /// Leva um resultado intermediário a 3 casas pelo arredondamento comum: 4ª casa ≥ 5 sobe (RN08).
    /// </summary>
    public static decimal ParaCalculo(decimal valor) =>
        Math.Round(valor, CasasCalculo, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Valor gravado e exibido, com 2 casas: primeiro 3 casas (<see cref="ParaCalculo"/>); depois a 3ª casa
    /// decide: maior que 5 sobe, 5 ou menor desce. Não usa o Math.Round padrão, que segue outra regra.
    /// </summary>
    public static decimal ParaGravacao(decimal valor)
    {
        var tresCasas = ParaCalculo(valor);
        var absoluto = Math.Abs(tresCasas);

        var duasCasas = Math.Truncate(absoluto * 100) / 100;
        var terceiraCasa = (int)(Math.Truncate(absoluto * 1000) % 10);
        if (terceiraCasa >= LimiarArredondamento)
        {
            duasCasas += 0.01m;
        }

        return (Math.Sign(tresCasas) * duasCasas) + EscalaDuasCasas;
    }

    /// <summary>Soma de valores já com 2 casas (ex.: total = soma dos subtotais arredondados).</summary>
    public static decimal Somar(IEnumerable<decimal> valores) => valores.Sum() + EscalaDuasCasas;
}
