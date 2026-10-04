using Moq;
using ProjectPricing.Aplicacao.Projetos;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Tests.Projetos;

public class ServicoProjetosTests
{
    private const string Id = "6703f1c2a900000000000001";
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);

    private readonly Mock<IProjetoRepository> _projetos = new();
    private readonly Mock<IConversaRepository> _conversas = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    public ServicoProjetosTests()
    {
        _usuario.SetupGet(u => u.KeycloakId).Returns("dono");
    }

    private ServicoProjetos Servico
    {
        get
        {
            var relogio = new Mock<TimeProvider>();
            relogio.Setup(r => r.GetUtcNow()).Returns(Agora);
            return new ServicoProjetos(_projetos.Object, _conversas.Object, _usuario.Object, relogio.Object);
        }
    }

    [Fact]
    public async Task Criar_grava_projeto_em_rascunho_do_usuario_e_a_conversa_vazia()
    {
        _projetos
            .Setup(p => p.InserirAsync(It.IsAny<Projeto>(), It.IsAny<CancellationToken>()))
            .Callback<Projeto, CancellationToken>((projeto, _) => DefinirId(projeto));

        var rodada = await Servico.CriarAsync("Placa 60x60", CancellationToken.None);

        Assert.Equal("dono", rodada.Projeto.ClienteId);
        Assert.Equal(StatusProjeto.Rascunho, rodada.Projeto.Status);
        Assert.Equal(Agora, rodada.Projeto.CriadoEm);
        Assert.Equal(Id, rodada.Conversa.ProjetoId);
        Assert.Empty(rodada.Conversa.Mensagens);
        _conversas.Verify(c => c.InserirAsync(rodada.Conversa, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("dono", false, true)]
    [InlineData("outro", true, true)]
    [InlineData("outro", false, false)]
    public async Task Obter_respeita_a_visibilidade_RN07(string usuario, bool admin, bool visivel)
    {
        ProjetoExistente();
        _usuario.SetupGet(u => u.KeycloakId).Returns(usuario);
        _usuario.SetupGet(u => u.EhAdmin).Returns(admin);

        if (visivel)
        {
            Assert.Equal(Id, (await Servico.ObterAsync(Id, CancellationToken.None)).Id);
        }
        else
        {
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Servico.ObterAsync(Id, CancellationToken.None));
        }
    }

    [Theory]
    [InlineData(false, "dono")]
    [InlineData(true, null)]
    public async Task Listagem_do_cliente_traz_so_os_proprios_e_a_do_admin_traz_todos(bool admin, string? clienteFiltrado)
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(admin);

        await Servico.ListarAsync(StatusProjeto.Cotado, 2, 10, CancellationToken.None);

        _projetos.Verify(p => p.ListarAsync(new FiltroProjetos(clienteFiltrado, StatusProjeto.Cotado, 2, 10), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Arquivar_e_exclusao_logica_e_nao_repete_a_gravacao()
    {
        var projeto = ProjetoExistente();

        await Servico.ArquivarAsync(Id, CancellationToken.None);
        await Servico.ArquivarAsync(Id, CancellationToken.None);

        Assert.Equal(StatusProjeto.Arquivado, projeto.Status);
        Assert.Equal(Agora, projeto.AlteradoEm);
        _projetos.Verify(p => p.AtualizarAsync(projeto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_pode_arquivar_projeto_de_cliente()
    {
        var projeto = ProjetoExistente();
        _usuario.SetupGet(u => u.KeycloakId).Returns("admin");
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);

        await Servico.ArquivarAsync(Id, CancellationToken.None);

        Assert.Equal(StatusProjeto.Arquivado, projeto.Status);
    }

    [Fact]
    public async Task Mensagem_so_do_dono_mesmo_o_admin_nao_conversa_no_projeto_alheio()
    {
        ProjetoExistente();
        _usuario.SetupGet(u => u.KeycloakId).Returns("admin");
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Servico.PrepararMensagemAsync(Id, CancellationToken.None));
    }

    [Fact]
    public async Task Mensagem_no_projeto_arquivado_e_recusada_RN11()
    {
        var projeto = ProjetoExistente();
        projeto.Arquivar(Agora);

        await Assert.ThrowsAsync<ProjetoArquivadoException>(() => Servico.PrepararMensagemAsync(Id, CancellationToken.None));
    }

    [Fact]
    public async Task Preparar_mensagem_devolve_o_projeto_e_a_conversa()
    {
        var projeto = ProjetoExistente();
        var conversa = ConversaExistente();

        var rodada = await Servico.PrepararMensagemAsync(Id, CancellationToken.None);

        Assert.Same(projeto, rodada.Projeto);
        Assert.Same(conversa, rodada.Conversa);
    }

    [Fact]
    public async Task Historico_omite_os_registros_internos_do_agente()
    {
        ProjetoExistente();
        var conversa = ConversaExistente();
        conversa.AdicionarMensagem(new Mensagem(PapelMensagem.Usuario, "Placa", Agora));
        conversa.AdicionarMensagem(new Mensagem(PapelMensagem.Tool, """{"sugestoes":[]}""", Agora));
        conversa.AdicionarMensagem(new Mensagem(PapelMensagem.Assistente, "Confirma?", Agora));

        var historico = await Servico.HistoricoAsync(Id, CancellationToken.None);

        Assert.Equal([PapelMensagem.Usuario, PapelMensagem.Assistente], historico.Select(m => m.Papel));
    }

    [Fact]
    public async Task Projeto_sem_conversa_e_erro_de_consistencia()
    {
        ProjetoExistente();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Servico.HistoricoAsync(Id, CancellationToken.None));
    }

    private Projeto ProjetoExistente()
    {
        var projeto = new Projeto("dono", "Placa", Agora.AddDays(-1));
        DefinirId(projeto);
        projeto.RegistrarCotacao([new ItemProjeto("m", "Chapa", 1m, UnidadeMedida.Unidade, 10m, 10m)], 10m, Agora.AddDays(-1));
        _projetos.Setup(p => p.ObterPorIdAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(projeto);
        return projeto;
    }

    private Conversa ConversaExistente()
    {
        var conversa = new Conversa(Id);
        _conversas.Setup(c => c.ObterPorProjetoIdAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(conversa);
        return conversa;
    }

    private static void DefinirId(Projeto projeto) =>
        typeof(Projeto).GetProperty(nameof(Projeto.Id))!.SetValue(projeto, Id);
}
