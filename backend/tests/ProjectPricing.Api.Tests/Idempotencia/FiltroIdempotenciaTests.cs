using System.Text;
using Microsoft.AspNetCore.Http;
using Moq;
using ProjectPricing.Api.Idempotencia;
using ProjectPricing.Api.Projetos;
using ProjectPricing.Aplicacao.Idempotencia;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Api.Tests.Idempotencia;

public class FiltroIdempotenciaTests
{
    private const string Chave = "7f1c9c1e-0f4a-4b1e-9d57-4c0f3b1a2e11";
    private const string Sse = "event: cotacao\ndata: {\"id\":\"p1\"}\n\nevent: fim\ndata: {}\n\n";

    private readonly Mock<IArmazemIdempotencia> _armazem = new();
    private int _execucoes;

    [Fact]
    public async Task Sem_o_cabecalho_o_filtro_nao_interfere()
    {
        var (http, corpo) = NovoContexto(chave: null);

        await Executar(http, EscreverSse);

        Assert.Equal(1, _execucoes);
        Assert.Equal(Sse, Encoding.UTF8.GetString(corpo.ToArray()));
        _armazem.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Primeira_chamada_processa_guarda_a_resposta_e_repassa_o_stream()
    {
        _armazem.Setup(a => a.ReservarAsync(Chave, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoReserva(SituacaoReserva.Nova));
        var (http, corpo) = NovoContexto();

        await Executar(http, async c =>
        {
            c.Response.StatusCode = 201;
            c.Response.Headers.Location = "/api/v1/projetos/p1";
            await EscreverSse(c);
        });

        Assert.Equal(1, _execucoes);
        Assert.Equal(Sse, Encoding.UTF8.GetString(corpo.ToArray()));
        _armazem.Verify(a => a.ConcluirAsync(Chave, new RespostaGuardada(201, "/api/v1/projetos/p1", Sse), It.IsAny<CancellationToken>()), Times.Once);
        _armazem.Verify(a => a.LiberarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Repeticao_reenvia_a_resposta_guardada_sem_processar_de_novo()
    {
        _armazem.Setup(a => a.ReservarAsync(Chave, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoReserva(SituacaoReserva.Repetida, new RespostaGuardada(201, "/api/v1/projetos/p1", Sse)));
        var (http, corpo) = NovoContexto();

        await Executar(http, EscreverSse);

        Assert.Equal(0, _execucoes);
        Assert.Equal(201, http.Response.StatusCode);
        Assert.Equal("/api/v1/projetos/p1", http.Response.Headers.Location.ToString());
        Assert.Equal("true", http.Response.Headers[FiltroIdempotencia.CabecalhoReenvio].ToString());
        Assert.Equal(Sse, Encoding.UTF8.GetString(corpo.ToArray()));
    }

    [Fact]
    public async Task Chave_reutilizada_com_outro_conteudo_e_erro_do_cliente()
    {
        _armazem.Setup(a => a.ReservarAsync(Chave, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoReserva(SituacaoReserva.Conflito));
        var (http, _) = NovoContexto();

        var erro = await Assert.ThrowsAsync<ChaveIdempotenciaReutilizadaException>(() => Executar(http, EscreverSse));

        Assert.Equal("IDEMPOTENCIA_CONFLITO", erro.Codigo);
        Assert.Equal(0, _execucoes);
    }

    [Fact]
    public async Task Chamada_simultanea_com_a_mesma_chave_recebe_conflito_de_andamento()
    {
        _armazem.Setup(a => a.ReservarAsync(Chave, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoReserva(SituacaoReserva.EmAndamento));
        var (http, _) = NovoContexto();

        await Assert.ThrowsAsync<RequisicaoEmAndamentoException>(() => Executar(http, EscreverSse));

        Assert.Equal(0, _execucoes);
    }

    [Fact]
    public async Task Falha_transitoria_libera_a_chave_para_nova_tentativa()
    {
        _armazem.Setup(a => a.ReservarAsync(Chave, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoReserva(SituacaoReserva.Nova));
        var (http, _) = NovoContexto();

        await Executar(http, async c =>
        {
            var bytes = Encoding.UTF8.GetBytes("event: erro\ndata: {\"status\":500,\"code\":\"FALHA_PROCESSAMENTO\"}\n\n");
            await c.Response.Body.WriteAsync(bytes);
        });

        _armazem.Verify(a => a.LiberarAsync(Chave, It.IsAny<CancellationToken>()), Times.Once);
        _armazem.Verify(a => a.ConcluirAsync(It.IsAny<string>(), It.IsAny<RespostaGuardada>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Erro_de_negocio_deterministico_e_guardado()
    {
        _armazem.Setup(a => a.ReservarAsync(Chave, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoReserva(SituacaoReserva.Nova));
        var (http, _) = NovoContexto();

        await Executar(http, async c =>
        {
            var bytes = Encoding.UTF8.GetBytes("event: erro\ndata: {\"status\":422,\"code\":\"ITENS_NAO_ENCONTRADOS\"}\n\n");
            await c.Response.Body.WriteAsync(bytes);
        });

        _armazem.Verify(a => a.ConcluirAsync(Chave, It.IsAny<RespostaGuardada>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Excecao_antes_do_stream_libera_a_chave_e_propaga()
    {
        _armazem.Setup(a => a.ReservarAsync(Chave, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoReserva(SituacaoReserva.Nova));
        var (http, _) = NovoContexto();

        await Assert.ThrowsAsync<ProjetoArquivadoException>(() => Executar(http, _ => throw new ProjetoArquivadoException()));

        _armazem.Verify(a => a.LiberarAsync(Chave, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("chave com espaço")]
    [InlineData("acentuação-ç")]
    public async Task Chave_invalida_e_recusada_antes_de_reservar(string chave)
    {
        var (http, _) = NovoContexto(chave);

        var resultado = await Executar(http, EscreverSse);

        Assert.IsAssignableFrom<IResult>(resultado);
        Assert.Equal(0, _execucoes);
        _armazem.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Chave_longa_demais_e_recusada()
    {
        var (http, _) = NovoContexto(new string('a', FiltroIdempotencia.TamanhoMaximoChave + 1));

        await Executar(http, EscreverSse);

        Assert.Equal(0, _execucoes);
        _armazem.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Impressao_digital_muda_com_o_conteudo_da_requisicao()
    {
        var impressoes = new List<string>();
        _armazem.Setup(a => a.ReservarAsync(Chave, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, impressao, _) => impressoes.Add(impressao))
            .ReturnsAsync(new ResultadoReserva(SituacaoReserva.Nova));

        await Executar(NovoContexto().Http, EscreverSse, new CriarProjetoRequisicao("Placa 60x60"));
        await Executar(NovoContexto().Http, EscreverSse, new CriarProjetoRequisicao("Placa 60x60"));
        await Executar(NovoContexto().Http, EscreverSse, new CriarProjetoRequisicao("Placa 80x80"));

        Assert.Equal(impressoes[0], impressoes[1]);
        Assert.NotEqual(impressoes[0], impressoes[2]);
    }

    private static (HttpContext Http, MemoryStream Corpo) NovoContexto(string? chave = Chave)
    {
        var http = new DefaultHttpContext();
        var corpo = new MemoryStream();
        http.Response.Body = corpo;
        http.Request.Method = "POST";
        http.Request.Path = "/api/v1/projetos";
        if (chave is not null)
        {
            http.Request.Headers[FiltroIdempotencia.Cabecalho] = chave;
        }

        return (http, corpo);
    }

    private static async Task EscreverSse(HttpContext http)
    {
        await http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Sse));
    }

    private async Task<object?> Executar(HttpContext http, Func<HttpContext, Task> handler, object? requisicao = null)
    {
        var contexto = requisicao is null
            ? EndpointFilterInvocationContext.Create(http)
            : EndpointFilterInvocationContext.Create(http, requisicao);
        var filtro = new FiltroIdempotencia(_armazem.Object);

        return await filtro.InvokeAsync(contexto, async c =>
        {
            _execucoes++;
            await handler(c.HttpContext);
            return TypedResults.Empty;
        });
    }
}
