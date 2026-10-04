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

    [Fact]
    public void Alterar_troca_os_dados_e_a_data_sem_mudar_o_status()
    {
        var material = NovoMaterial();
        material.Inativar(Agora.AddHours(1));

        material.Alterar(" Película premium ", ["película"], TipoMaterial.Material, "Sinalização",
            UnidadeMedida.MetroQuadrado, 99.90m, "Outro", Agora.AddHours(2));

        Assert.Equal("Película premium", material.Nome);
        Assert.Equal(["película"], material.Sinonimos);
        Assert.Equal(99.90m, material.PrecoUnitario);
        Assert.Equal("Outro", material.Fornecedor);
        Assert.Equal(StatusMaterial.Inativo, material.Status);
        Assert.Equal(Agora.AddHours(2), material.AtualizadoEm);
    }

    [Fact]
    public void Alterar_valida_como_no_cadastro()
    {
        var material = NovoMaterial();

        Assert.Throws<ArgumentOutOfRangeException>(() => material.Alterar(
            "Nome", [], TipoMaterial.Material, "C", UnidadeMedida.Unidade, 0m, "F", Agora));
    }

    [Fact]
    public void Inativar_e_reativar_registram_a_data()
    {
        var material = NovoMaterial();

        material.Inativar(Agora.AddDays(1));
        Assert.Equal(StatusMaterial.Inativo, material.Status);
        Assert.Equal(Agora.AddDays(1), material.AtualizadoEm);

        material.Reativar(Agora.AddDays(2));
        Assert.Equal(StatusMaterial.Ativo, material.Status);
        Assert.Equal(Agora.AddDays(2), material.AtualizadoEm);
    }

    [Fact]
    public void Nome_e_sinonimos_sao_os_termos_unicos_do_material()
    {
        var material = new Material("Chapa", ["placa", "lâmina"], TipoMaterial.Material, "C",
            UnidadeMedida.MetroQuadrado, 1m, "F", Agora);

        Assert.Equal(["Chapa", "placa", "lâmina"], material.NomeESinonimos);
    }

    private static Material NovoMaterial() =>
        new("Película refletiva", [], TipoMaterial.Material, "Sinalização", UnidadeMedida.MetroQuadrado, 95m, "Refletivos", Agora);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Preco_unitario_precisa_ser_positivo(decimal preco)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Material(
            "Nome", [], TipoMaterial.Material, "Categoria", UnidadeMedida.Unidade, preco, "Fornecedor", Agora));
    }
}
