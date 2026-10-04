using Moq;
using ProjectPricing.Aplicacao.Materiais;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Aplicacao.Tests.Materiais;

public class ServicoCatalogoTests
{
    private const string Id = "6703e0aa0000000000000001";
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);
    private static readonly DadosMaterial Dados = new(
        "Película refletiva", ["tinta reflexiva"], TipoMaterial.Material, "Sinalização",
        UnidadeMedida.MetroQuadrado, 95.00m, "Refletivos SA");

    private readonly Mock<IMaterialRepository> _repositorio = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();
    private readonly Mock<TimeProvider> _relogio = new();

    public ServicoCatalogoTests()
    {
        _relogio.Setup(r => r.GetUtcNow()).Returns(Agora);
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);
    }

    private ServicoCatalogo Servico => new(_repositorio.Object, _usuario.Object, _relogio.Object);

    [Fact]
    public async Task Criar_grava_material_ativo_com_a_data_atual()
    {
        var material = await Servico.CriarAsync(Dados, CancellationToken.None);

        Assert.Equal("Película refletiva", material.Nome);
        Assert.Equal(StatusMaterial.Ativo, material.Status);
        Assert.Equal(Agora, material.AtualizadoEm);
        _repositorio.Verify(r => r.InserirAsync(material, CancellationToken.None), Times.Once);
        _repositorio.Verify(r => r.BuscarConflitoDeNomeAsync(
            It.Is<IReadOnlyCollection<string>>(t => t.SequenceEqual(new[] { "Película refletiva", "tinta reflexiva" })),
            null,
            CancellationToken.None));
    }

    [Theory]
    [InlineData("PELICULA REFLETIVA", "Película refletiva")]
    [InlineData("Tinta Reflexiva", "tinta reflexiva")]
    public async Task Criar_com_nome_ou_sinonimo_repetido_informa_o_termo(string termoExistente, string termoInformado)
    {
        var existente = new Material(termoExistente, [], TipoMaterial.Material, "C", UnidadeMedida.Unidade, 1m, "F", Agora);
        _repositorio
            .Setup(r => r.BuscarConflitoDeNomeAsync(It.IsAny<IReadOnlyCollection<string>>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);

        var erro = await Assert.ThrowsAsync<MaterialDuplicadoException>(() => Servico.CriarAsync(Dados, CancellationToken.None));

        Assert.Equal(termoInformado, erro.TermoDuplicado);
        _repositorio.Verify(r => r.InserirAsync(It.IsAny<Material>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Alterar_ignora_o_proprio_material_na_duplicidade_e_pode_inativar()
    {
        var material = MaterialExistente();

        var alterado = await Servico.AlterarAsync(Id, Dados with { PrecoUnitario = 99.90m }, StatusMaterial.Inativo, CancellationToken.None);

        Assert.Same(material, alterado);
        Assert.Equal(99.90m, alterado.PrecoUnitario);
        Assert.Equal(StatusMaterial.Inativo, alterado.Status);
        _repositorio.Verify(r => r.BuscarConflitoDeNomeAsync(It.IsAny<IReadOnlyCollection<string>>(), Id, CancellationToken.None));
        _repositorio.Verify(r => r.AtualizarAsync(material, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Alterar_com_status_ativo_reativa()
    {
        var material = MaterialExistente();
        material.Inativar(Agora.AddDays(-1));

        await Servico.AlterarAsync(Id, Dados, StatusMaterial.Ativo, CancellationToken.None);

        Assert.Equal(StatusMaterial.Ativo, material.Status);
        Assert.Equal(Agora, material.AtualizadoEm);
    }

    [Fact]
    public async Task Alterar_ou_inativar_material_inexistente_e_404()
    {
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            Servico.AlterarAsync(Id, Dados, StatusMaterial.Ativo, CancellationToken.None));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            Servico.InativarAsync(Id, CancellationToken.None));
    }

    [Fact]
    public async Task Inativar_e_exclusao_logica()
    {
        var material = MaterialExistente();

        await Servico.InativarAsync(Id, CancellationToken.None);

        Assert.Equal(StatusMaterial.Inativo, material.Status);
        _repositorio.Verify(r => r.AtualizarAsync(material, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Cliente_nao_enxerga_material_inativo()
    {
        var material = MaterialExistente();
        material.Inativar(Agora);
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Servico.ObterAsync(Id, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_enxerga_material_inativo()
    {
        var material = MaterialExistente();
        material.Inativar(Agora);

        Assert.Same(material, await Servico.ObterAsync(Id, CancellationToken.None));
    }

    [Fact]
    public async Task Listagem_do_cliente_e_sempre_do_catalogo_ativo()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);
        var filtro = new FiltroMateriais("placa", null, StatusMaterial.Inativo, 1, 20);

        var esperado = filtro with { Status = StatusMaterial.Ativo };

        await Servico.ListarAsync(filtro, CancellationToken.None);

        _repositorio.Verify(r => r.ListarAsync(esperado, CancellationToken.None));
    }

    [Fact]
    public async Task Listagem_do_admin_usa_o_filtro_pedido()
    {
        var filtro = new FiltroMateriais(null, "Sinalização", null, 2, 10);
        var pagina = new PaginaDeMateriais([], 0);
        _repositorio.Setup(r => r.ListarAsync(filtro, CancellationToken.None)).ReturnsAsync(pagina);

        Assert.Same(pagina, await Servico.ListarAsync(filtro, CancellationToken.None));
    }

    private Material MaterialExistente()
    {
        var material = new Material("Película refletiva", [], TipoMaterial.Material, "Sinalização",
            UnidadeMedida.MetroQuadrado, 95m, "Refletivos SA", Agora.AddDays(-10));
        _repositorio.Setup(r => r.ObterPorIdAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(material);
        return material;
    }
}
