using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProjectPricing.Api.Sse;
using ProjectPricing.Aplicacao.Idempotencia;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Api.Idempotencia;

/// <summary>Corpo de requisição que participa da checagem de idempotência (a chave só vale para o mesmo conteúdo).</summary>
public interface IRequisicaoIdempotente;

/// <summary>
/// Idempotência dos POSTs que acionam o agente (ADR-012). Com o cabeçalho <c>Idempotency-Key</c>, a primeira chamada
/// é processada e sua resposta SSE é guardada; repetições (duplo clique, retry de rede, reconexão) recebem a mesma
/// resposta sem criar outro projeto nem chamar de novo o LLM, que é pago. Sem o cabeçalho, nada muda.
/// </summary>
public sealed class FiltroIdempotencia(IArmazemIdempotencia armazem) : IEndpointFilter
{
    public const string Cabecalho = "Idempotency-Key";
    public const string CabecalhoReenvio = "Idempotent-Replayed";
    public const int TamanhoMaximoChave = 128;

    /// <summary>Resultados que não devem ser reaproveitados: o reenvio da mesma chave precisa tentar de novo.</summary>
    private static readonly string[] CodigosTransitorios = ["FALHA_PROCESSAMENTO", "ERRO_INTERNO"];

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!http.Request.Headers.TryGetValue(Cabecalho, out var valores) || valores.Count == 0)
        {
            return await next(context);
        }

        var chave = valores.ToString().Trim();
        if (!ChaveValida(chave))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [Cabecalho] = [$"Use de 1 a {TamanhoMaximoChave} caracteres ASCII visíveis."],
            });
        }

        var cancelamento = http.RequestAborted;
        var reserva = await armazem.ReservarAsync(chave, ImpressaoDigital(context), cancelamento);
        switch (reserva.Situacao)
        {
            case SituacaoReserva.Conflito:
                throw new ChaveIdempotenciaReutilizadaException();
            case SituacaoReserva.EmAndamento:
                throw new RequisicaoEmAndamentoException();
            case SituacaoReserva.Repetida:
                await ReenviarAsync(http, reserva.Resposta!, cancelamento);
                return TypedResults.Empty;
            default:
                return await ProcessarEGuardarAsync(context, next, chave);
        }
    }

    private static bool ChaveValida(string chave) =>
        chave.Length is >= 1 and <= TamanhoMaximoChave && chave.All(c => c is > ' ' and <= '~');

    /// <summary>Método, caminho e corpo: a mesma chave com outro projeto ou outra mensagem é erro do cliente.</summary>
    private static string ImpressaoDigital(EndpointFilterInvocationContext context)
    {
        var requisicao = context.HttpContext.Request;
        var corpo = context.Arguments.OfType<IRequisicaoIdempotente>().FirstOrDefault();
        var json = corpo is null ? string.Empty : JsonSerializer.Serialize(corpo, corpo.GetType());
        var texto = $"{requisicao.Method} {requisicao.Path}\n{json}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }

    private async ValueTask<object?> ProcessarEGuardarAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        string chave)
    {
        var http = context.HttpContext;
        var original = http.Response.Body;
        await using var espelho = new FluxoEspelho(original);
        http.Response.Body = espelho;

        var concluiu = false;
        try
        {
            var resultado = await next(context);

            // Só a resposta SSE, escrita pelo próprio handler, passa pelo espelho. Respostas HTTP comuns
            // (404, 422 antes do stream etc.) são executadas depois do filtro e não são guardadas.
            var texto = espelho.Texto();
            if (espelho.TotalGravado > 0 && !EhFalhaTransitoria(texto))
            {
                var location = http.Response.Headers.Location.ToString();
                await armazem.ConcluirAsync(
                    chave,
                    new RespostaGuardada(http.Response.StatusCode, location.Length > 0 ? location : null, texto),
                    CancellationToken.None);
                concluiu = true;
            }

            return resultado;
        }
        finally
        {
            http.Response.Body = original;
            if (!concluiu)
            {
                await armazem.LiberarAsync(chave, CancellationToken.None);
            }
        }
    }

    private static bool EhFalhaTransitoria(string sse) =>
        CodigosTransitorios.Any(codigo => sse.Contains($"\"code\":\"{codigo}\"", StringComparison.Ordinal));

    private static async Task ReenviarAsync(HttpContext http, RespostaGuardada resposta, CancellationToken cancellationToken)
    {
        http.Response.StatusCode = resposta.StatusHttp;
        http.Response.ContentType = EscritorSse.TipoConteudo;
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Response.Headers[CabecalhoReenvio] = "true";
        if (resposta.Location is not null)
        {
            http.Response.Headers.Location = resposta.Location;
        }

        await http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(resposta.Corpo), cancellationToken);
        await http.Response.Body.FlushAsync(cancellationToken);
    }
}

public static class ConfiguracaoIdempotencia
{
    public static RouteHandlerBuilder Idempotente(this RouteHandlerBuilder endpoint) =>
        endpoint.AddEndpointFilter<FiltroIdempotencia>();
}
