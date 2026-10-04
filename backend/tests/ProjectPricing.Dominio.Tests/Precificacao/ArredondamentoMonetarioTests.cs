using System.Globalization;
using ProjectPricing.Dominio.Precificacao;

namespace ProjectPricing.Dominio.Tests.Precificacao;

/// <summary>Tabela da RN08 (business-rules.md §5).</summary>
public class ArredondamentoMonetarioTests
{
    [Theory]
    [InlineData("43.206", "43.21")] // 3ª casa = 6, sobe
    [InlineData("43.209", "43.21")] // 3ª casa = 9, sobe
    [InlineData("43.205", "43.20")] // 3ª casa = 5, desce
    [InlineData("43.201", "43.20")] // 3ª casa = 1, desce
    [InlineData("9.5725", "9.57")] // 4ª casa = 5: vira 9,573; 3ª casa = 3, desce
    [InlineData("0.16666", "0.17")] // 10 min em h: vira 0,167; 3ª casa = 7, sobe
    [InlineData("14.45", "14.45")] // 0,17 h × R$ 85,00
    [InlineData("0.1089", "0.11")] // 33 cm × 33 cm: vira 0,109; 3ª casa = 9, sobe
    public void Casos_da_tabela_RN08(string calculado, string gravado)
    {
        Assert.Equal(D(gravado), ArredondamentoMonetario.ParaGravacao(D(calculado)));
    }

    [Theory]
    [InlineData("9.5725", "9.573")]
    [InlineData("0.16666", "0.167")]
    [InlineData("0.1089", "0.109")]
    [InlineData("0.1084", "0.108")]
    public void Calculo_usa_tres_casas_pelo_arredondamento_comum(string valor, string esperado)
    {
        Assert.Equal(D(esperado), ArredondamentoMonetario.ParaCalculo(D(valor)));
    }

    [Fact]
    public void Limiar_da_terceira_casa_e_6()
    {
        Assert.Equal(6, ArredondamentoMonetario.LimiarArredondamento);
    }

    [Fact]
    public void Valor_gravado_sempre_mostra_os_centavos()
    {
        Assert.Equal("43.20", ArredondamentoMonetario.ParaGravacao(43.2m).ToString(CultureInfo.InvariantCulture));
        Assert.Equal("105.90", ArredondamentoMonetario.Somar([43.20m, 34.20m, 28.50m]).ToString(CultureInfo.InvariantCulture));
        Assert.Equal("0.00", ArredondamentoMonetario.Somar([]).ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Valores_negativos_arredondam_de_forma_simetrica()
    {
        Assert.Equal(-43.21m, ArredondamentoMonetario.ParaGravacao(-43.206m));
        Assert.Equal(-43.20m, ArredondamentoMonetario.ParaGravacao(-43.205m));
    }

    [Fact]
    public void Nao_e_o_Math_Round_padrao()
    {
        // O Math.Round padrão (meio para o par) daria 43,22 para 43,215; a RN08 desce, porque a 3ª casa é 5.
        Assert.Equal(43.21m, ArredondamentoMonetario.ParaGravacao(43.215m));
        Assert.Equal(43.22m, Math.Round(43.215m, 2));
    }

    private static decimal D(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);
}
