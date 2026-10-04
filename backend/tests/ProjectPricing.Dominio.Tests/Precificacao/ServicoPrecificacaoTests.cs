using Moq;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Precificacao;

namespace ProjectPricing.Dominio.Tests.Precificacao;

public class ServicoPrecificacaoTests
{
    private readonly ServicoPrecificacao _servico = new(ConversorUnidades.CriarPadrao());

    [Fact]
    public void Exemplo_da_placa_de_transito_da_documentacao()
    {
        var placa60x60 = new MedidaInformada(null, "cm", 60, 60);

        var resultado = _servico.Calcular(
        [
            new ItemACalcular("m1", "Chapa de aço galvanizado", UnidadeMedida.MetroQuadrado, 120.00m, placa60x60),
            new ItemACalcular("m2", "Película refletiva", UnidadeMedida.MetroQuadrado, 95.00m, placa60x60),
            new ItemACalcular("m3", "Cabeçote de metal", UnidadeMedida.Unidade, 28.50m, new MedidaInformada(1, "un")),
        ]);

        Assert.Equal(["m1", "m2", "m3"], resultado.Itens.Select(i => i.MaterialId));
        Assert.Equal("Película refletiva", resultado.Itens[1].Nome);
        Assert.Equal([43.20m, 34.20m, 28.50m], resultado.Itens.Select(i => i.Subtotal));
        Assert.Equal([0.36m, 0.36m, 1m], resultado.Itens.Select(i => i.Quantidade));
        Assert.Equal(105.90m, resultado.Total);
        Assert.Equal("105.90", resultado.Total.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Quantidade_entra_no_calculo_com_duas_casas_igual_a_exibida()
    {
        // 10 min = 0,1666… h → 0,17 h; 0,17 × 85,00 = 14,45 (e não 14,19, que sairia de 0,167 h).
        var item = _servico.CalcularItem(
            new ItemACalcular("s1", "Instalação", UnidadeMedida.Hora, 85.00m, new MedidaInformada(10, "min")));

        Assert.Equal(0.17m, item.Quantidade);
        Assert.Equal(14.45m, item.Subtotal);
        Assert.Equal(UnidadeMedida.Hora, item.Unidade);
        Assert.Equal(85.00m, item.PrecoUnitario);
    }

    [Theory]
    [InlineData("0.35", "27.35", "9.57")] // 9,5725 → 9,573; 3ª casa = 3, desce
    [InlineData("0.36", "120.00", "43.20")]
    [InlineData("1.5", "0.01", "0.01")] // 0,015; 3ª casa = 5, desce
    [InlineData("0.7", "0.01", "0.01")] // 0,007; 3ª casa = 7, sobe
    [InlineData("0.5", "0.13", "0.06")] // 0,065; 3ª casa = 5, desce
    public void Subtotal_segue_a_RN08(string quantidade, string preco, string esperado)
    {
        var item = _servico.CalcularItem(new ItemACalcular(
            "m", "Material", UnidadeMedida.MetroQuadrado, D(preco), new MedidaInformada(D(quantidade), "m2")));

        Assert.Equal(D(esperado), item.Subtotal);
    }

    private static decimal D(string valor) => decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Area_de_33_por_33_cm_vira_0_11_m2()
    {
        var item = _servico.CalcularItem(new ItemACalcular(
            "m", "Película", UnidadeMedida.MetroQuadrado, 100.00m, new MedidaInformada(null, "cm", 33, 33)));

        Assert.Equal(0.11m, item.Quantidade);
        Assert.Equal(11.00m, item.Subtotal);
    }

    [Fact]
    public void Quantidade_que_zera_ao_arredondar_e_medida_invalida()
    {
        var erro = Assert.Throws<MedidaInvalidaException>(() => _servico.CalcularItem(new ItemACalcular(
            "m", "Película", UnidadeMedida.MetroQuadrado, 95.00m, new MedidaInformada(1, "mm2"))));

        Assert.Contains("Película", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Preco_precisa_ser_positivo()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _servico.CalcularItem(new ItemACalcular(
            "m", "Material", UnidadeMedida.Unidade, 0m, new MedidaInformada(1, "un"))));
    }

    [Fact]
    public void Totalizar_soma_subtotais_ja_arredondados()
    {
        Assert.Equal(105.90m, _servico.Totalizar([43.20m, 34.20m, 28.50m]));
    }

    [Fact]
    public void Lista_vazia_tem_total_zero()
    {
        var resultado = _servico.Calcular([]);

        Assert.Empty(resultado.Itens);
        Assert.Equal(0.00m, resultado.Total);
    }

    [Fact]
    public void Usa_o_conversor_injetado()
    {
        var conversor = new Mock<IConversorUnidades>();
        conversor.Setup(c => c.Converter(It.IsAny<MedidaInformada>(), UnidadeMedida.Litro)).Returns(2.5m);

        var item = new ServicoPrecificacao(conversor.Object).CalcularItem(
            new ItemACalcular("t", "Tinta", UnidadeMedida.Litro, 40.00m, new MedidaInformada(2500, "ml")));

        Assert.Equal(100.00m, item.Subtotal);
    }
}
