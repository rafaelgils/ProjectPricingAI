using System.Security.Claims;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Tenancy;

namespace ProjectPricing.Api.Tenancy;

/// <summary>Lê o tenant da claim <c>tenant_id</c> do JWT validado, emitida pelo Keycloak (ADR-011).</summary>
public sealed class TenantAtualHttp(IHttpContextAccessor acessorHttp) : ITenantAtual
{
    public const string Claim = "tenant_id";

    public string TenantId => acessorHttp.HttpContext?.User.FindFirstValue(Claim) is { Length: > 0 } tenant
        ? tenant
        : throw new TenantNaoIdentificadoException();
}
