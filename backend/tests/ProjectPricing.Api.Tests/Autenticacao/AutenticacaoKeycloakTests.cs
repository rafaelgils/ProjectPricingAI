using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectPricing.Api.Autenticacao;

namespace ProjectPricing.Api.Tests.Autenticacao;

public class AutenticacaoKeycloakTests
{
    [Fact]
    public void Le_metadados_pela_rede_interna_e_valida_o_emissor_publico()
    {
        var opcoes = ConfigurarJwt(new()
        {
            ["Keycloak:UrlPublica"] = "http://localhost:8080/realms/precificacao/",
            ["Keycloak:UrlInterna"] = "http://keycloak:8080/realms/precificacao",
            ["Keycloak:Audiencia"] = "precificacao-api",
            ["Keycloak:ExigirHttpsMetadata"] = "false",
        });

        Assert.Equal("http://keycloak:8080/realms/precificacao/.well-known/openid-configuration", opcoes.MetadataAddress);
        Assert.Equal("http://localhost:8080/realms/precificacao", opcoes.TokenValidationParameters.ValidIssuer);
        Assert.Equal("precificacao-api", opcoes.TokenValidationParameters.ValidAudience);
        Assert.Equal(ClaimTypes.Role, opcoes.TokenValidationParameters.RoleClaimType);
        Assert.False(opcoes.RequireHttpsMetadata);
        Assert.False(opcoes.MapInboundClaims);
    }

    [Fact]
    public void Sem_url_interna_usa_a_publica_e_exige_HTTPS_por_padrao()
    {
        var opcoes = ConfigurarJwt(new()
        {
            ["Keycloak:UrlPublica"] = "https://login.exemplo/realms/precificacao",
            ["Keycloak:Audiencia"] = "precificacao-api",
        });

        Assert.Equal("https://login.exemplo/realms/precificacao/.well-known/openid-configuration", opcoes.MetadataAddress);
        Assert.True(opcoes.RequireHttpsMetadata);
    }

    [Theory]
    [InlineData(null, "precificacao-api")]
    [InlineData("http://localhost:8080/realms/precificacao", null)]
    public void Url_publica_e_audiencia_sao_obrigatorias(string? urlPublica, string? audiencia)
    {
        var configuracao = new Dictionary<string, string?>
        {
            ["Keycloak:UrlPublica"] = urlPublica,
            ["Keycloak:Audiencia"] = audiencia,
        };

        Assert.Throws<InvalidOperationException>(() => ConfigurarJwt(configuracao));
    }

    [Fact]
    public void Configuracao_sem_a_secao_Keycloak_falha_na_subida()
    {
        Assert.Throws<InvalidOperationException>(() => ConfigurarJwt([]));
    }

    [Fact]
    public async Task Token_validado_ganha_os_papeis_do_realm()
    {
        var opcoes = ConfigurarJwt(new()
        {
            ["Keycloak:UrlPublica"] = "http://localhost:8080/realms/precificacao",
            ["Keycloak:Audiencia"] = "precificacao-api",
            ["Keycloak:ExigirHttpsMetadata"] = "false",
        });
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PapeisKeycloak.ClaimRealmAccess, """{"roles":["cliente-externo"]}""")],
            "Bearer", "preferred_username", ClaimTypes.Role));
        var esquema = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var contexto = new TokenValidatedContext(new DefaultHttpContext(), esquema, opcoes) { Principal = principal };

        await opcoes.Events.OnTokenValidated(contexto);

        Assert.True(principal.IsInRole("cliente-externo"));
    }

    private static JwtBearerOptions ConfigurarJwt(Dictionary<string, string?> valores)
    {
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AdicionarAutenticacaoKeycloak(configuracao);

        return services.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
    }
}
