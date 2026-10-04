using System.Text;
using System.Text.Json;

namespace ProjectPricing.Api.Sse;

/// <summary>Eventos SSE da cotação e do refinamento (standards.md §5).</summary>
public static class EventosSse
{
    public const string Delta = "delta";
    public const string Cotacao = "cotacao";
    public const string Erro = "erro";
    public const string Fim = "fim";
}

/// <summary>
/// Escreve a resposta em text/event-stream, com o data de cada evento em JSON e envio imediato
/// (sem buffer), para a primeira parte chegar ao cliente em até 3 s (RNF02).
/// </summary>
public sealed class EscritorSse(HttpResponse resposta, JsonSerializerOptions opcoesJson)
{
    public const string TipoConteudo = "text/event-stream";

    /// <summary>Envia o status e os cabeçalhos. Depois disso, erros só podem ir como evento.</summary>
    public async Task IniciarAsync(int status, string? location, CancellationToken cancellationToken)
    {
        resposta.StatusCode = status;
        resposta.ContentType = TipoConteudo;
        resposta.Headers.CacheControl = "no-cache";
        // Pede aos proxies (Nginx, Kong) que não acumulem o stream.
        resposta.Headers["X-Accel-Buffering"] = "no";
        if (location is not null)
        {
            resposta.Headers.Location = location;
        }

        await resposta.Body.FlushAsync(cancellationToken);
    }

    public async Task EnviarAsync(string evento, object dados, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(dados, dados.GetType(), opcoesJson);
        var bytes = Encoding.UTF8.GetBytes($"event: {evento}\ndata: {json}\n\n");
        await resposta.Body.WriteAsync(bytes, cancellationToken);
        await resposta.Body.FlushAsync(cancellationToken);
    }
}
