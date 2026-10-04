using Moq;
using ProjectPricing.Api.Materiais;
using ProjectPricing.Aplicacao.Materiais;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Api.Tests.Materiais;

public class MateriaisEndpointsTests
{
    private const string Id = "6703e0aa0000000000000001";
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);

    private static readonly AlterarMaterialRequisicao Requisicao = new(
        "Película refletiva", [], TipoMaterial.Material, "Sinalização",
        UnidadeMedida.MetroQuadrado, 95.00m, "Refletivos SA", StatusMaterial.Ativo);

    private readonly Mock<IMaterialRepository> _repositorio = new();
    private readonly ServicoCatalogo _servico;

    public MateriaisEndpointsTests()
    {
        var relogio = new Mock<TimeProvider>();
        relogio.Setup(r => r.GetUtcNow()).Returns(Agora);
        _servico = new ServicoCatalogo(_repositorio.Object, Mock.Of<IUsuarioAtual>(u => u.EhAdmin), relogio.Object);
    }

    [Fact]
    public async Task Criar_responde_201_com_Location_e_moeda()
    {
        _repositorio
            .Setup(r => r.InserirAsync(It.IsAny<Material>(), It.IsAny<CancellationToken>()))
            .Callback<Material, CancellationToken>((material, _) => DefinirId(material, Id));

        var resposta = await MateriaisEndpoints.Criar(Requisicao, _servico, CancellationToken.None);

        Assert.Equal(201, resposta.StatusCode);
        Assert.Equal($"/api/v1/materiais/{Id}", resposta.Location);
        Assert.Equal(Id, resposta.Value!.Id);
        Assert.Equal("BRL", resposta.Value.Moeda);
        Assert.Equal(Agora, resposta.Value.AtualizadoEm);
    }

    [Fact]
    public async Task Listar_devolve_a_pagina_com_total()
    {
        _repositorio
            .Setup(r => r.ListarAsync(It.IsAny<FiltroMateriais>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginaDeMateriais([ComId(NovoMaterial())], 41));

        var resposta = await MateriaisEndpoints.Listar(new ConsultaMateriais(null, null, null, 3, 20), _servico, CancellationToken.None);

        Assert.Equal(200, resposta.StatusCode);
        Assert.Equal(3, resposta.Value!.Pagina);
        Assert.Equal(20, resposta.Value.Tamanho);
        Assert.Equal(41, resposta.Value.Total);
        Assert.Equal(Id, Assert.Single(resposta.Value.Itens).Id);
    }

    [Fact]
    public async Task Obter_alterar_e_inativar()
    {
        var material = ComId(NovoMaterial());
        _repositorio.Setup(r => r.ObterPorIdAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(material);

        var obtido = await MateriaisEndpoints.Obter(Id, _servico, CancellationToken.None);
        var alterado = await MateriaisEndpoints.Alterar(Id, Requisicao with { PrecoUnitario = 99.90m, Status = StatusMaterial.Inativo }, _servico, CancellationToken.None);
        var inativado = await MateriaisEndpoints.Inativar(Id, _servico, CancellationToken.None);

        Assert.Equal(200, obtido.StatusCode);
        Assert.Equal(99.90m, alterado.Value!.PrecoUnitario);
        Assert.Equal(StatusMaterial.Inativo, alterado.Value.Status);
        Assert.Equal(204, inativado.StatusCode);
    }

    [Fact]
    public void Resposta_exige_material_gravado()
    {
        Assert.Throws<InvalidOperationException>(() => MaterialResposta.De(NovoMaterial()));
    }

    private static Material NovoMaterial() =>
        new("Película refletiva", [], TipoMaterial.Material, "Sinalização", UnidadeMedida.MetroQuadrado, 95m, "Refletivos SA", Agora);

    private static Material ComId(Material material)
    {
        DefinirId(material, Id);
        return material;
    }

    // O id é atribuído pelo banco; nos testes, por reflexão.
    private static void DefinirId(Material material, string id) =>
        typeof(Material).GetProperty(nameof(Material.Id))!.SetValue(material, id);
}
