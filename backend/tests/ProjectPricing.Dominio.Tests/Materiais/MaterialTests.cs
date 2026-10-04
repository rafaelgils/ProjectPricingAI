using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Dominio.Tests.Materiais;

public class MaterialTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 14, 30, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Novo_material_fica_ativo_e_sem_espacos_extras()
    {
        var material = new Material(
            "  Chapa de aço galvanizado ",
            [" placa ", "chapa"],
            TipoMaterial.Material,
            " Sinalização ",
            UnidadeMedida.MetroQuadrado,
            120.00m,
            " Aço Brasil ",
            Agora);

        Assert.Null(material.Id);
        Assert.Equal("Chapa de aço galvanizado", material.Nome);
        Assert.Equal(["placa", "chapa"], material.Sinonimos);
        Assert.Equal(TipoMaterial.Material, material.Tipo);
        Assert.Equal("Sinalização", material.Categoria);
        Assert.Equal(UnidadeMedida.MetroQuadrado, material.Unidade);
        Assert.Equal(120.00m, material.PrecoUnitario);
        Assert.Equal("Aço Brasil", material.Fornecedor);
        Assert.Equal(StatusMaterial.Ativo, material.Status);
        Assert.Equal(Agora, material.AtualizadoEm);
    }

    [Theory]
    [InlineData("", "Categoria", "Fornecedor")]
    [InlineData("Nome", " ", "Fornecedor")]
    [InlineData("Nome", "Categoria", "")]
    public void Campos_de_texto_obrigatorios(string nome, string categoria, string fornecedor)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Material(
            nome, [], TipoMaterial.Servico, categoria, UnidadeMedida.Hora, 10m, fornecedor, Agora));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Preco_unitario_precisa_ser_positivo(decimal preco)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Material(
            "Nome", [], TipoMaterial.Material, "Categoria", UnidadeMedida.Unidade, preco, "Fornecedor", Agora));
    }
}
