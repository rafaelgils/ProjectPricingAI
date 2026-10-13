using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ProjectPricing.Api.Erros;
using ProjectPricing.Api.Tenancy;

namespace ProjectPricing.Api.Limites;

/// <summary>Seção "LimitesDeUso" da configuração (ADR-013). Os valores padrão valem se a seção não existir.</summary>
public sealed class OpcoesLimitesDeUso
{
    public const string Secao = "LimitesDeUso";

    /// <summary>Qualquer rota da API, por usuário (ou por IP, se anônimo).</summary>
    public int RequisicoesPorMinutoPorUsuario { get; init; } = 120;

    /// <summary>Cotações e mensagens ao agente, por usuário: cada uma é uma chamada paga ao LLM.</summary>
    public int CotacoesPorMinutoPorUsuario { get; init; } = 10;

    /// <summary>Cotações e mensagens ao agente somadas na empresa: teto de custo por tenant.</summary>
    public int CotacoesPorHoraPorTenant { get; init; } = 300;
}

/// <summary>
/// Limites de uso no backend (ADR-013): protegem o custo da Claude API e a disponibilidade, por usuário e por tenant.
/// Complementam o <c>rate-limiting</c> por IP do Kong, que atua antes de a requisição chegar aqui.
/// </summary>
public static class LimitesDeUso
{
    public const string CodigoErro = "LIMITE_DE_USO_EXCEDIDO";

    public static IServiceCollection AdicionarLimitesDeUso(this IServiceCollection services, OpcoesLimitesDeUso opcoes)
    {
        services.AddRateLimiter(limitador =>
        {
            limitador.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limitador.GlobalLimiter = CriarLimitador(opcoes);
            limitador.OnRejected = ResponderLimiteExcedido;
        });

        return services;
    }

    /// <summary>Cotações e mensagens: os dois POSTs que acionam o agente (e, portanto, o LLM).</summary>
    public static bool AcionaOAgente(HttpRequest requisicao)
    {
        if (!HttpMethods.IsPost(requisicao.Method))
        {
            return false;
        }

        var caminho = requisicao.Path.Value?.TrimEnd('/') ?? string.Empty;
        const string Base = "/api/v1/projetos";
        return caminho.Equals(Base, StringComparison.OrdinalIgnoreCase)
            || (caminho.StartsWith(Base + "/", StringComparison.OrdinalIgnoreCase)
                && caminho.EndsWith("/mensagens", StringComparison.OrdinalIgnoreCase));
    }

    private static PartitionedRateLimiter<HttpContext> CriarLimitador(OpcoesLimitesDeUso opcoes) =>
        PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter($"geral:{ChaveDoUsuario(http)}", _ => Janela(opcoes.RequisicoesPorMinutoPorUsuario, TimeSpan.FromMinutes(1)))),
            PartitionedRateLimiter.Create<HttpContext, string>(http =>
                AcionaOAgente(http.Request)
                    ? RateLimitPartition.GetFixedWindowLimiter($"agente-usuario:{ChaveDoUsuario(http)}", _ => Janela(opcoes.CotacoesPorMinutoPorUsuario, TimeSpan.FromMinutes(1)))
                    : RateLimitPartition.GetNoLimiter("sem-limite")),
            PartitionedRateLimiter.Create<HttpContext, string>(http =>
                AcionaOAgente(http.Request) && http.User.FindFirstValue(TenantAtualHttp.Claim) is { Length: > 0 } tenant
                    ? RateLimitPartition.GetFixedWindowLimiter($"agente-tenant:{tenant}", _ => Janela(opcoes.CotacoesPorHoraPorTenant, TimeSpan.FromHours(1)))
                    : RateLimitPartition.GetNoLimiter("sem-limite")));

    private static FixedWindowRateLimiterOptions Janela(int limite, TimeSpan janela) => new()
    {
        PermitLimit = limite,
        Window = janela,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    /// <summary>Usuário autenticado (<c>sub</c>) ou, sem token, o IP de origem.</summary>
    private static string ChaveDoUsuario(HttpContext http) =>
        http.User.FindFirstValue("sub") is { Length: > 0 } sub
            ? $"u:{sub}"
            : $"ip:{http.Connection.RemoteIpAddress}";

    private static async ValueTask ResponderLimiteExcedido(OnRejectedContext contexto, CancellationToken cancellationToken)
    {
        var resposta = contexto.HttpContext.Response;
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
        {
            resposta.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        resposta.StatusCode = StatusCodes.Status429TooManyRequests;
        await resposta.WriteAsJsonAsync(
            new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Type = CodigosErro.Tipo(CodigoErro),
                Title = "Muitas requisições. Aguarde um instante e tente novamente.",
                Extensions = { [MapeadorDeErros.ExtensaoCodigo] = CodigoErro },
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    }
}
