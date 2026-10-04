using FluentValidation;
using ProjectPricing.Api.Materiais;
using ProjectPricing.Api.Validacao;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Api.Tests.Materiais;

public class ValidadoresMaterialTests
{
    private static readonly CriarMaterialRequisicao Valida = new(
        "Película refletiva", ["tinta reflexiva"], TipoMaterial.Material, "Sinalização",
        UnidadeMedida.MetroQuadrado, 95.00m, "Refletivos SA");

    static ValidadoresMaterialTests()
    {
        ConfiguracaoValidacao.Aplicar();
    }

    [Fact]
    public void Requisicao_completa_e_valida()
    {
        Assert.True(new CriarMaterialValidador().Validate(Valida).IsValid);
    }

    [Fact]
    public void Campos_obrigatorios_ausentes_sao_apontados_em_camelCase()
    {
        var resultado = new CriarMaterialValidador().Validate(new CriarMaterialRequisicao(null, null, null, null, null, null, null));

        Assert.Equal(
            ["categoria", "fornecedor", "nome", "precoUnitario", "tipo", "unidade"],
            resultado.Errors.Select(e => e.PropertyName).Distinct().Order());
        Assert.Contains(resultado.Errors, e => e.ErrorMessage == "Informe o nome.");
    }

    [Theory]
    [InlineData(1.234, false)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(99.90, true)]
    [InlineData(10.500, true)]
    public void Preco_positivo_com_no_maximo_duas_casas(decimal preco, bool valido)
    {
        var resultado = new CriarMaterialValidador().Validate(Valida with { PrecoUnitario = preco });

        Assert.Equal(valido, resultado.IsValid);
    }

    [Theory]
    [InlineData("Placa", "PLACA")]
    [InlineData("pelicula refletiva")]
    [InlineData("")]
    public void Sinonimos_nao_podem_repetir_nem_ser_vazios(params string[] sinonimos)
    {
        var resultado = new CriarMaterialValidador().Validate(Valida with { Sinonimos = sinonimos });

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void No_maximo_vinte_sinonimos()
    {
        var sinonimos = Enumerable.Range(1, CriarMaterialValidador.MaximoSinonimos + 1).Select(i => $"s{i}").ToArray();

        Assert.False(new CriarMaterialValidador().Validate(Valida with { Sinonimos = sinonimos }).IsValid);
    }

    [Fact]
    public void Alteracao_exige_status_e_reaproveita_as_regras_do_cadastro()
    {
        var validador = new AlterarMaterialValidador();
        var semStatus = new AlterarMaterialRequisicao("Nome", [], TipoMaterial.Servico, "C", UnidadeMedida.Hora, 85m, "F", null);
        var semNome = semStatus with { Nome = "", Status = StatusMaterial.Ativo };

        Assert.Contains(validador.Validate(semStatus).Errors, e => e.PropertyName == "status");
        Assert.Contains(validador.Validate(semNome).Errors, e => e.PropertyName == "nome");
        Assert.True(validador.Validate(semStatus with { Status = StatusMaterial.Inativo }).IsValid);
    }

    [Theory]
    [InlineData(null, null, null, true)]
    [InlineData("ativo", 1, 100, true)]
    [InlineData("Ativo", null, null, false)]
    [InlineData(null, 0, null, false)]
    [InlineData(null, null, 101, false)]
    public void Consulta_valida_status_pagina_e_tamanho(string? status, int? pagina, int? tamanho, bool valida)
    {
        var resultado = new ConsultaMateriaisValidador().Validate(new ConsultaMateriais(null, null, status, pagina, tamanho));

        Assert.Equal(valida, resultado.IsValid);
    }

    [Fact]
    public void Consulta_sem_paginacao_usa_pagina_1_e_tamanho_20()
    {
        var filtro = new ConsultaMateriais("placa", "Sinalização", "inativo", null, null).ParaFiltro();

        Assert.Equal(new FiltroMateriais("placa", "Sinalização", StatusMaterial.Inativo, 1, 20), filtro);
        Assert.Null(new ConsultaMateriais(null, null, null, 2, 10).ParaFiltro().Status);
    }

    [Fact]
    public void Requisicao_valida_vira_dados_do_servico()
    {
        var dados = (Valida with { Sinonimos = null }).ParaDados();

        Assert.Empty(dados.Sinonimos);
        Assert.Equal(95.00m, dados.PrecoUnitario);
        Assert.Equal(UnidadeMedida.MetroQuadrado, dados.Unidade);
    }
}
