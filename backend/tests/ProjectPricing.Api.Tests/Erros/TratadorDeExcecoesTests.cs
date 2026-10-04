using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectPricing.Api.Erros;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Api.Tests.Erros;

public class TratadorDeExcecoesTests
{
    private readonly Mock<IProblemDetailsService> _servicoProblemas = new();
    private readonly Mock<ILogger<TratadorDeExcecoes>> _logger = new();
    private ProblemDetailsContext? _escrito;

    public TratadorDeExcecoesTests()
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _servicoProblemas
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(contexto => _escrito = contexto)
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task Regra_de_negocio_responde_422_sem_log_de_erro()
    {
        var http = new DefaultHttpContext();

        var tratado = await Tratar(http, new ProjetoArquivadoException());

        Assert.True(tratado);
        Assert.Equal(422, http.Response.StatusCode);
        Assert.Equal("PROJETO_ARQUIVADO", _escrito!.ProblemDetails.Extensions["code"]);
        _logger.Verify(Log(LogLevel.Error), Times.Never);
    }

    [Fact]
    public async Task Falha_do_LLM_responde_500_e_registra_a_causa_no_log()
    {
        var http = new DefaultHttpContext();
        var causa = new TimeoutException("LLM demorou");

        await Tratar(http, new FalhaProcessamentoException(causa));

        Assert.Equal(500, http.Response.StatusCode);
        _logger.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), causa, It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private Task<bool> Tratar(HttpContext http, Exception erro) =>
        new TratadorDeExcecoes(_servicoProblemas.Object, _logger.Object)
            .TryHandleAsync(http, erro, CancellationToken.None).AsTask();

    private static System.Linq.Expressions.Expression<Action<ILogger<TratadorDeExcecoes>>> Log(LogLevel nivel) =>
        l => l.Log(nivel, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>());
}
