using System.Globalization;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Precificacao;

namespace ProjectPricing.Dominio.Tests.Precificacao;

/// <summary>Tabela da RN05 (business-rules.md §5), linha a linha.</summary>
public class ConversorUnidadesTests
{
    private readonly ConversorUnidades _conversor = ConversorUnidades.CriarPadrao();

    [Theory]
    // m
    [InlineData("150", "cm", UnidadeMedida.Metro, "1.5")]
    [InlineData("1500", "mm", UnidadeMedida.Metro, "1.5")]
    [InlineData("2", "m", UnidadeMedida.Metro, "2")]
    // m2 por área
    [InlineData("3600", "cm²", UnidadeMedida.MetroQuadrado, "0.36")]
    [InlineData("360000", "mm2", UnidadeMedida.MetroQuadrado, "0.36")]
    [InlineData("1.5", "m2", UnidadeMedida.MetroQuadrado, "1.5")]
    // L
    [InlineData("500", "ml", UnidadeMedida.Litro, "0.5")]
    [InlineData("2", "L", UnidadeMedida.Litro, "2")]
    [InlineData("2", "l", UnidadeMedida.Litro, "2")]
    // h
    [InlineData("90", "min", UnidadeMedida.Hora, "1.5")]
    [InlineData("2", "h", UnidadeMedida.Hora, "2")]
    // un
    [InlineData("3", "un", UnidadeMedida.Unidade, "3")]
    public void Converte_quantidade_para_a_unidade_do_material(string quantidade, string unidade, UnidadeMedida destino, string esperado)
    {
        var convertido = _conversor.Converter(new MedidaInformada(D(quantidade), unidade), destino);

        Assert.Equal(D(esperado), ArredondamentoMonetario.ParaCalculo(convertido));
    }

    [Theory]
    [InlineData("60", "60", "cm", null, "0.36")] // placa 60 x 60 cm
    [InlineData("600", "600", "mm", null, "0.36")]
    [InlineData("80", "80", "cm", null, "0.64")] // "troque para 80x80"
    [InlineData("60", "60", "cm", "2", "0.72")] // 2 placas
    [InlineData("1.2", "0.5", "m", null, "0.6")]
    public void Calcula_area_a_partir_de_largura_e_altura(string largura, string altura, string unidade, string? pecas, string esperado)
    {
        var medida = new MedidaInformada(pecas is null ? null : D(pecas), unidade, D(largura), D(altura));

        Assert.Equal(D(esperado), _conversor.Converter(medida, UnidadeMedida.MetroQuadrado));
    }

    public static TheoryData<MedidaInformada, UnidadeMedida> MedidasInvalidas => new()
    {
        { new MedidaInformada(500, "ml"), UnidadeMedida.Metro }, // unidade incompatível
        { new MedidaInformada(2, "km"), UnidadeMedida.Metro }, // unidade fora da tabela
        { new MedidaInformada(2, "cm"), UnidadeMedida.MetroQuadrado }, // linear sem a outra dimensão
        { new MedidaInformada(null, "un"), UnidadeMedida.Unidade }, // quantidade ausente
        { new MedidaInformada(0, "h"), UnidadeMedida.Hora }, // quantidade zero
        { new MedidaInformada(-1, "L"), UnidadeMedida.Litro }, // quantidade negativa
        { new MedidaInformada(null, "cm", 60, null), UnidadeMedida.MetroQuadrado }, // só largura
        { new MedidaInformada(null, "cm", 60, 0), UnidadeMedida.MetroQuadrado }, // altura zero
        { new MedidaInformada(null, "cm2", 60, 60), UnidadeMedida.MetroQuadrado }, // dimensões em unidade de área
        { new MedidaInformada(0, "cm", 60, 60), UnidadeMedida.MetroQuadrado }, // zero peças
        { new MedidaInformada(1, "un", 60, 60), UnidadeMedida.Unidade }, // dimensões fora de m²
        { new MedidaInformada(1, null!), UnidadeMedida.Unidade }, // unidade ausente
    };

    [Theory]
    [MemberData(nameof(MedidasInvalidas))]
    public void Medida_que_nao_pode_ser_convertida_e_erro_de_dominio(MedidaInformada medida, UnidadeMedida destino)
    {
        var erro = Assert.Throws<MedidaInvalidaException>(() => _conversor.Converter(medida, destino));

        Assert.Equal("MEDIDA_INVALIDA", erro.Codigo);
    }

    [Fact]
    public void Mensagem_indica_a_unidade_do_material()
    {
        var erro = Assert.Throws<MedidaInvalidaException>(() =>
            _conversor.Converter(new MedidaInformada(500, "ml"), UnidadeMedida.MetroQuadrado));

        Assert.Equal("A unidade \"ml\" não pode ser convertida para m2.", erro.Message);
    }

    [Fact]
    public void Unidade_sem_regra_e_erro_de_configuracao()
    {
        var conversor = new ConversorUnidades([new RegraUnidade()]);

        Assert.Throws<InvalidOperationException>(() => conversor.Converter(new MedidaInformada(1, "h"), UnidadeMedida.Hora));
    }

    private static decimal D(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);
}
