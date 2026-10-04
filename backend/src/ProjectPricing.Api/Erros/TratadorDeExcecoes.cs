using Microsoft.AspNetCore.Diagnostics;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Api.Erros;

/// <summary>Responde qualquer exceção não tratada com Problem Details; detalhes internos só vão para o log.</summary>
public sealed partial class TratadorDeExcecoes(
    IProblemDetailsService servicoProblemas,
    ILogger<TratadorDeExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problema = MapeadorDeErros.Mapear(exception);
        var status = problema.Status ?? StatusCodes.Status500InternalServerError;

        if (status >= StatusCodes.Status500InternalServerError)
        {
            var causa = (exception as FalhaProcessamentoException)?.Causa ?? exception;
            LogErroInterno(logger, causa);
        }

        httpContext.Response.StatusCode = status;
        return await servicoProblemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
            Exception = exception,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro ao processar a requisição.")]
    private static partial void LogErroInterno(ILogger logger, Exception excecao);
}
