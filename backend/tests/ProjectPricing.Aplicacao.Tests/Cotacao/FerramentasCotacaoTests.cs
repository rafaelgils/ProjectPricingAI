using Moq;
using ProjectPricing.Aplicacao.Cotacao;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Precificacao;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Tests.Cotacao;

public class FerramentasCotacaoTests
{
    private const string ProjetoId = "6703f1c2a900000000000001";
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);
    private static readonly MedidaInformada Placa60x60 = new(null, "cm", 60, 60);

    private readonly Mock<IMaterialRepository> _materiais = new();
    private readonly Mock<IProjetoRepository> _projetos = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();
    private readonly Projeto _projeto;

    public FerramentasCotacaoTests()
    {
        _usuario.SetupGet(u => u.KeycloakId).Returns("cliente-1");
        _projeto = new Projeto("cliente-1", "Placa", Agora.AddDays(-1));
        typeof(Projeto).GetProperty(nameof(Projeto.Id))!.SetValue(_projeto, ProjetoId);
        _projetos.Setup(p => p.ObterPorIdAsync(ProjetoId, It.IsAny<CancellationToken>())).ReturnsAsync(_projeto);
    }

    private FerramentasCotacao Ferramentas
    {
        get
        {
            var relogio = new Mock<TimeProvider>();
            relogio.Setup(r => r.GetUtcNow()).Returns(Agora);
            return new FerramentasCotacao(_materiais.Object, _projetos.Object,
                new ServicoPrecificacao(ConversorUnidades.CriarPadrao()), _usuario.Object, relogio.Object);
        }
    }

    [Fact]
    public async Task Buscar_classifica_cada_termo_no_catalogo_ativo()
    {
        _materiais.Setup(m => m.ListarAtivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Material("m-chapa", "Chapa de aço galvanizado", 120m, "placa")]);

        var resultados = await Ferramentas.BuscarMateriaisAsync(["placa", "parafuso"], CancellationToken.None);

        Assert.Equal(ClassificacaoTermo.Encontrado, resultados[0].Classificacao);
        Assert.Equal("m-chapa", resultados[0].Material!.Id);
        Assert.Equal(ClassificacaoTermo.SemCorrespondencia, resultados[1].Classificacao);
    }

    [Fact]
    public async Task Calcular_material_novo_usa_o_preco_atual_do_catalogo()
    {
        CatalogoTem(Material("m-chapa", "Chapa de aço galvanizado", 120m));

        var resultado = await Ferramentas.CalcularAsync(ProjetoId, [new ItemPedido("m-chapa", Placa60x60)], CancellationToken.None);

        Assert.Equal(43.20m, resultado.Total);
        Assert.Equal("Chapa de aço galvanizado", resultado.Itens.Single().Nome);
    }

    [Fact]
    public async Task Material_ja_cotado_mantem_nome_e_preco_congelados_RN02()
    {
        _projeto.RegistrarCotacao([new ItemProjeto("m-chapa", "Chapa antiga", 0.36m, UnidadeMedida.MetroQuadrado, 100m, 36.00m)], 36.00m, Agora);
        CatalogoTem(Material("m-chapa", "Chapa de aço galvanizado", 120m));

        var resultado = await Ferramentas.CalcularAsync(ProjetoId, [new ItemPedido("m-chapa", new MedidaInformada(null, "cm", 80, 80))], CancellationToken.None);

        var item = resultado.Itens.Single();
        Assert.Equal(100m, item.PrecoUnitario);
        Assert.Equal("Chapa antiga", item.Nome);
        Assert.Equal(64.00m, item.Subtotal);
    }

    [Fact]
    public async Task Material_novo_inativo_ou_inexistente_nao_entra_na_cotacao()
    {
        var inativo = Material("m-inativo", "Tinta antiga", 30m);
        inativo.Inativar(Agora);
        CatalogoTem(inativo);

        var erro = await Assert.ThrowsAsync<ItensNaoEncontradosException>(() => Ferramentas.CalcularAsync(
            ProjetoId, [new ItemPedido("m-inativo", new MedidaInformada(1, "m2")), new ItemPedido("m-sumiu", new MedidaInformada(1, "m2"))],
            CancellationToken.None));

        Assert.Equal(["m-inativo", "m-sumiu"], erro.ItensNaoEncontrados);
    }

    [Fact]
    public async Task Salvar_recalcula_e_grava_a_cotacao_com_snapshot()
    {
        CatalogoTem(Material("m-chapa", "Chapa de aço galvanizado", 120m), Material("m-cabecote", "Cabeçote de metal", 28.50m));

        var projeto = await Ferramentas.SalvarProjetoAsync(ProjetoId,
            [new ItemPedido("m-chapa", Placa60x60), new ItemPedido("m-cabecote", new MedidaInformada(1, "un"))],
            CancellationToken.None);

        Assert.Equal(StatusProjeto.Cotado, projeto.Status);
        Assert.Equal(71.70m, projeto.ValorTotal);
        Assert.Equal(Agora, projeto.AlteradoEm);
        Assert.Equal(120m, projeto.Itens[0].PrecoUnitarioSnapshot);
        Assert.Equal("Cabeçote de metal", projeto.Itens[1].NomeSnapshot);
        _projetos.Verify(p => p.AtualizarAsync(_projeto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Projeto_de_outro_cliente_responde_como_inexistente_RN07()
    {
        _usuario.SetupGet(u => u.KeycloakId).Returns("outro-cliente");

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            Ferramentas.CalcularAsync(ProjetoId, [], CancellationToken.None));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            Ferramentas.SalvarProjetoAsync("6703f1c2a9000000000000ff", [], CancellationToken.None));
    }

    [Fact]
    public async Task Projeto_arquivado_e_somente_leitura_RN11()
    {
        typeof(Projeto).GetProperty(nameof(Projeto.Status))!.SetValue(_projeto, StatusProjeto.Arquivado);

        await Assert.ThrowsAsync<ProjetoArquivadoException>(() =>
            Ferramentas.SalvarProjetoAsync(ProjetoId, [], CancellationToken.None));
    }

    [Fact]
    public async Task Catalogo_ativo_vem_do_repositorio()
    {
        var catalogo = new[] { Material("m-chapa", "Chapa", 120m) };
        _materiais.Setup(m => m.ListarAtivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(catalogo);

        Assert.Same(catalogo, await Ferramentas.ListarCatalogoAtivoAsync(CancellationToken.None));
    }

    private void CatalogoTem(params Material[] materiais) =>
        _materiais
            .Setup(m => m.ObterPorIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) => [.. materiais.Where(m => ids.Contains(m.Id!))]);

    private static Material Material(string id, string nome, decimal preco, params string[] sinonimos)
    {
        var unidade = nome.StartsWith("Cabeçote", StringComparison.Ordinal) ? UnidadeMedida.Unidade : UnidadeMedida.MetroQuadrado;
        var material = new Material(nome, sinonimos, TipoMaterial.Material, "Sinalização", unidade, preco, "Fornecedor", Agora);
        typeof(Material).GetProperty(nameof(Dominio.Materiais.Material.Id))!.SetValue(material, id);
        return material;
    }
}
