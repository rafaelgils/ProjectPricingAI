using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectPricing.Api.Projetos;
using ProjectPricing.Api.Validacao;
using ProjectPricing.Aplicacao.Agente;
using ProjectPricing.Aplicacao.Projetos;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Projetos;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace ProjectPricing.Api.Tests.Projetos;

public class ProjetosEndpointsTests
{
    private const string Id = "6703f1c2a900000000000001";
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);

    private readonly Mock<IProjetoRepository> _projetos = new();
    private readonly Mock<IConversaRepository> _conversas = new();
    private readonly Mock<IAgenteProjetos> _agente = new();
    private readonly ServicoProjetos _servico;
    private readonly CondutorConversa _condutor;

    public ProjetosEndpointsTests()
    {
        var relogio = new Mock<TimeProvider>();
        relogio.Setup(r => r.GetUtcNow()).Returns(Agora);
        _servico = new ServicoProjetos(_projetos.Object, _conversas.Object, Mock.Of<IUsuarioAtual>(u => u.KeycloakId == "dono"), relogio.Object);
        _condutor = new CondutorConversa(_agente.Object, Options.Create(new JsonOptions()), NullLogger<CondutorConversa>.Instance);
        _agente
            .Setup(a => a.ProcessarAsync(It.IsAny<Projeto>(), It.IsAny<Conversa>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FalhaProcessamentoException());
    }

    [Fact]
    public async Task Criar_grava_o_projeto_e_responde_201_em_SSE_com_Location()
    {
        _projetos
            .Setup(p => p.InserirAsync(It.IsAny<Projeto>(), It.IsAny<CancellationToken>()))
            .Callback<Projeto, CancellationToken>((projeto, _) => DefinirId(projeto));
        var (http, corpo) = NovoContexto();

        await ProjetosEndpoints.Criar(new CriarProjetoRequisicao("  Placa 60x60  "), _servico, _condutor, http, CancellationToken.None);

        Assert.Equal(201, http.Response.StatusCode);
        Assert.Equal($"/api/v1/projetos/{Id}", http.Response.Headers.Location.ToString());
        Assert.Contains("event: fim", Encoding.UTF8.GetString(corpo.ToArray()), StringComparison.Ordinal);
        _agente.Verify(a => a.ProcessarAsync(It.IsAny<Projeto>(), It.IsAny<Conversa>(), "Placa 60x60", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Mensagem_em_projeto_arquivado_falha_antes_de_abrir_o_stream()
    {
        var projeto = ProjetoExistente();
        projeto.Arquivar(Agora);
        var (http, _) = NovoContexto();

        await Assert.ThrowsAsync<ProjetoArquivadoException>(() =>
            ProjetosEndpoints.EnviarMensagem(Id, new EnviarMensagemRequisicao("Troque para 80x80"), _servico, _condutor, http, CancellationToken.None));

        Assert.False(http.Response.HasStarted);
        Assert.Equal(200, http.Response.StatusCode);
        Assert.Null(http.Response.ContentType);
    }

    [Fact]
    public async Task Mensagem_do_dono_responde_200_em_SSE()
    {
        ProjetoExistente();
        _conversas.Setup(c => c.ObterPorProjetoIdAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(new Conversa(Id));
        var (http, _) = NovoContexto();

        await ProjetosEndpoints.EnviarMensagem(Id, new EnviarMensagemRequisicao(" Troque para 80x80 "), _servico, _condutor, http, CancellationToken.None);

        Assert.Equal("text/event-stream", http.Response.ContentType);
        _agente.Verify(a => a.ProcessarAsync(It.IsAny<Projeto>(), It.IsAny<Conversa>(), "Troque para 80x80", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Listar_obter_arquivar_e_historico()
    {
        var projeto = ProjetoExistente();
        var conversa = new Conversa(Id);
        conversa.AdicionarMensagem(new Mensagem(PapelMensagem.Usuario, "Placa", Agora));
        _conversas.Setup(c => c.ObterPorProjetoIdAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(conversa);
        _projetos
            .Setup(p => p.ListarAsync(It.IsAny<FiltroProjetos>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginaDeProjetos([projeto], 1));

        var lista = await ProjetosEndpoints.Listar(new ConsultaProjetos("rascunho", null, null), _servico, CancellationToken.None);
        var detalhe = await ProjetosEndpoints.Obter(Id, _servico, CancellationToken.None);
        var historico = await ProjetosEndpoints.Historico(Id, _servico, CancellationToken.None);
        var arquivado = await ProjetosEndpoints.Arquivar(Id, _servico, CancellationToken.None);

        Assert.Equal(Id, Assert.Single(lista.Value!.Itens).Id);
        Assert.Equal(1, lista.Value.Pagina);
        Assert.Equal(20, lista.Value.Tamanho);
        Assert.Equal("BRL", detalhe.Value!.Moeda);
        Assert.Equal(PapelMensagem.Usuario, Assert.Single(historico.Value!).Papel);
        Assert.Equal(204, arquivado.StatusCode);
        _projetos.Verify(p => p.ListarAsync(new FiltroProjetos("dono", StatusProjeto.Rascunho, 1, 20), It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    [InlineData("Placa 60x60", true)]
    public void Descricao_e_obrigatoria(string? descricao, bool valida)
    {
        ConfiguracaoValidacao.Aplicar();

        Assert.Equal(valida, new CriarProjetoValidador().Validate(new CriarProjetoRequisicao(descricao)).IsValid);
        Assert.Equal(valida, new EnviarMensagemValidador().Validate(new EnviarMensagemRequisicao(descricao)).IsValid);
    }

    [Fact]
    public void Texto_acima_do_limite_e_recusado()
    {
        var longo = new string('a', LimitesTexto.MaximoMensagem + 1);

        Assert.False(new CriarProjetoValidador().Validate(new CriarProjetoRequisicao(longo)).IsValid);
    }

    [Theory]
    [InlineData("cotado", null, null, true)]
    [InlineData("Cotado", null, null, false)]
    [InlineData(null, 0, null, false)]
    [InlineData(null, null, 101, false)]
    public void Consulta_de_projetos_valida_status_e_paginacao(string? status, int? pagina, int? tamanho, bool valida)
    {
        var consulta = new ConsultaProjetos(status, pagina, tamanho);

        Assert.Equal(valida, new ConsultaProjetosValidador().Validate(consulta).IsValid);
    }

    private Projeto ProjetoExistente()
    {
        var projeto = new Projeto("dono", "Placa", Agora);
        DefinirId(projeto);
        _projetos.Setup(p => p.ObterPorIdAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(projeto);
        return projeto;
    }

    private static (HttpContext Http, MemoryStream Corpo) NovoContexto()
    {
        var corpo = new MemoryStream();
        return (new DefaultHttpContext { Response = { Body = corpo } }, corpo);
    }

    private static void DefinirId(Projeto projeto) =>
        typeof(Projeto).GetProperty(nameof(Projeto.Id))!.SetValue(projeto, Id);
}
