using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ProjectPricing.Api.Autorizacao;

namespace ProjectPricing.Api.Tests.Autorizacao;

/// <summary>Matriz de permissões de business-rules.md §3.</summary>
public class PoliticasTests
{
    private readonly IServiceProvider _servicos;

    public PoliticasTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AdicionarPoliticasDeAutorizacao();
        _servicos = services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(Politicas.PodeGerenciarCatalogo, "admin", true)]
    [InlineData(Politicas.PodeGerenciarCatalogo, "cliente-interno", false)]
    [InlineData(Politicas.PodeGerenciarCatalogo, "cliente-externo", false)]
    [InlineData(Politicas.PodeConsultarCatalogo, "admin", true)]
    [InlineData(Politicas.PodeConsultarCatalogo, "cliente-interno", true)]
    [InlineData(Politicas.PodeConsultarCatalogo, "cliente-externo", false)]
    [InlineData(Politicas.PodeCotar, "admin", true)]
    [InlineData(Politicas.PodeCotar, "cliente-interno", true)]
    [InlineData(Politicas.PodeCotar, "cliente-externo", true)]
    [InlineData(Politicas.PodeGerenciarUsuarios, "admin", true)]
    [InlineData(Politicas.PodeGerenciarUsuarios, "cliente-interno", false)]
    [InlineData(Politicas.PodeGerenciarUsuarios, "cliente-externo", false)]
    public async Task Permissao_por_papel(string politica, string papel, bool permitido)
    {
        var resultado = await Autorizar(UsuarioCom(papel), politica);

        Assert.Equal(permitido, resultado.Succeeded);
    }

    [Fact]
    public async Task Usuario_sem_papel_nao_pode_cotar()
    {
        var resultado = await Autorizar(UsuarioCom(), Politicas.PodeCotar);

        Assert.False(resultado.Succeeded);
    }

    [Fact]
    public async Task Endpoint_sem_policy_exige_usuario_autenticado()
    {
        var fallback = await _servicos.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        var servico = _servicos.GetRequiredService<IAuthorizationService>();

        Assert.NotNull(fallback);
        Assert.False((await servico.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), fallback)).Succeeded);
        Assert.True((await servico.AuthorizeAsync(UsuarioCom(), fallback)).Succeeded);
    }

    private Task<AuthorizationResult> Autorizar(ClaimsPrincipal usuario, string politica) =>
        _servicos.GetRequiredService<IAuthorizationService>().AuthorizeAsync(usuario, politica);

    private static ClaimsPrincipal UsuarioCom(params string[] papeis) =>
        new(new ClaimsIdentity(papeis.Select(p => new Claim(ClaimTypes.Role, p)), "Bearer"));
}
