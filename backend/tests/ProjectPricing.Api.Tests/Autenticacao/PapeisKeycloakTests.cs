using System.Security.Claims;
using ProjectPricing.Api.Autenticacao;

namespace ProjectPricing.Api.Tests.Autenticacao;

public class PapeisKeycloakTests
{
    [Fact]
    public void Le_os_realm_roles_da_claim_realm_access()
    {
        var papeis = PapeisKeycloak.LerPapeis("""{"roles":["admin","cliente-interno"]}""");

        Assert.Equal(["admin", "cliente-interno"], papeis);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nao é json")]
    [InlineData("""{"outra":["admin"]}""")]
    [InlineData("""{"roles":"admin"}""")]
    public void Claim_ausente_ou_malformada_nao_concede_papel(string? realmAccess)
    {
        Assert.Empty(PapeisKeycloak.LerPapeis(realmAccess));
    }

    [Fact]
    public void Ignora_itens_que_nao_sao_texto()
    {
        Assert.Equal(["admin"], PapeisKeycloak.LerPapeis("""{"roles":["admin",7,null]}"""));
    }

    [Fact]
    public void Adiciona_os_papeis_como_claims_de_papel_sem_duplicar()
    {
        var identidade = new ClaimsIdentity(
            [new Claim(PapeisKeycloak.ClaimRealmAccess, """{"roles":["admin"]}""")],
            authenticationType: "Bearer",
            nameType: "preferred_username",
            roleType: ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identidade);

        PapeisKeycloak.AdicionarPapeis(principal);
        PapeisKeycloak.AdicionarPapeis(principal);

        Assert.True(principal.IsInRole("admin"));
        Assert.Single(identidade.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Identidade_nao_autenticada_nao_recebe_papeis()
    {
        var identidade = new ClaimsIdentity([new Claim(PapeisKeycloak.ClaimRealmAccess, """{"roles":["admin"]}""")]);
        var principal = new ClaimsPrincipal(identidade);

        PapeisKeycloak.AdicionarPapeis(principal);

        Assert.False(principal.IsInRole("admin"));
    }
}
