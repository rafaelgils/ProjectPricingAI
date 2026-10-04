using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ProjectPricing.Api.Autenticacao;

public static class AutenticacaoKeycloak
{
    private const string CaminhoMetadados = "/.well-known/openid-configuration";

    /// <summary>
    /// Valida o JWT também no Backend (RNF01, ADR-004): assinatura pelas chaves do realm,
    /// emissor público, audiência <c>precificacao-api</c> e expiração.
    /// </summary>
    public static IServiceCollection AdicionarAutenticacaoKeycloak(
        this IServiceCollection services,
        IConfiguration configuracao)
    {
        var opcoes = configuracao.GetSection(OpcoesKeycloak.Secao).Get<OpcoesKeycloak>()
            ?? throw new InvalidOperationException($"Seção \"{OpcoesKeycloak.Secao}\" ausente na configuração.");

        if (string.IsNullOrWhiteSpace(opcoes.UrlPublica) || string.IsNullOrWhiteSpace(opcoes.Audiencia))
        {
            throw new InvalidOperationException("Keycloak:UrlPublica e Keycloak:Audiencia são obrigatórios.");
        }

        var urlMetadados = (string.IsNullOrWhiteSpace(opcoes.UrlInterna) ? opcoes.UrlPublica : opcoes.UrlInterna)
            .TrimEnd('/') + CaminhoMetadados;

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                // Mantém os nomes das claims do Keycloak (sub, email, realm_access).
                jwt.MapInboundClaims = false;
                jwt.MetadataAddress = urlMetadados;
                jwt.RequireHttpsMetadata = opcoes.ExigirHttpsMetadata;
                jwt.TokenValidationParameters.ValidIssuer = opcoes.UrlPublica.TrimEnd('/');
                jwt.TokenValidationParameters.ValidAudience = opcoes.Audiencia;
                jwt.TokenValidationParameters.NameClaimType = "preferred_username";
                jwt.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = contexto =>
                    {
                        if (contexto.Principal is not null)
                        {
                            PapeisKeycloak.AdicionarPapeis(contexto.Principal);
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        return services;
    }
}
