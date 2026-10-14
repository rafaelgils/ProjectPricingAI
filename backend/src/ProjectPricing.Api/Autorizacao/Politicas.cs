using Microsoft.AspNetCore.Authorization;
using ProjectPricing.Api.Tenancy;
using ProjectPricing.Aplicacao.Usuarios;

namespace ProjectPricing.Api.Autorizacao;

/// <summary>Policies por papel (business-rules.md §3). Os endpoints usam só estes nomes.</summary>
public static class Politicas
{
    public const string PodeGerenciarCatalogo = nameof(PodeGerenciarCatalogo);
    public const string PodeConsultarCatalogo = nameof(PodeConsultarCatalogo);
    public const string PodeCotar = nameof(PodeCotar);
    public const string PodeGerenciarUsuarios = nameof(PodeGerenciarUsuarios);

    public static IServiceCollection AdicionarPoliticasDeAutorizacao(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Qualquer endpoint sem policy explícita exige usuário autenticado.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(TenantAtualHttp.Claim)
                .Build())
            // Toda policy exige também o tenant: sem empresa, o usuário não acessa nenhum recurso (ADR-011).
            .AddPolicy(PodeGerenciarCatalogo, p => p.RequireClaim(TenantAtualHttp.Claim).RequireRole(Papeis.Admin))
            .AddPolicy(PodeConsultarCatalogo, p => p.RequireClaim(TenantAtualHttp.Claim).RequireRole(Papeis.Admin, Papeis.ClienteInterno))
            .AddPolicy(PodeCotar, p => p.RequireClaim(TenantAtualHttp.Claim).RequireRole(Papeis.Admin, Papeis.ClienteInterno, Papeis.ClienteExterno))
            .AddPolicy(PodeGerenciarUsuarios, p => p.RequireClaim(TenantAtualHttp.Claim).RequireRole(Papeis.Admin));

        return services;
    }
}
