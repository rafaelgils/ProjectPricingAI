using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using ProjectPricing.Api.Usuarios;
using ProjectPricing.Aplicacao.Usuarios;

namespace ProjectPricing.Api.Tests.Usuarios;

public class UsuarioAtualHttpTests
{
    [Fact]
    public void Le_os_dados_do_usuario_das_claims_do_token()
    {
        var usuario = UsuarioDaRequisicao(
            new Claim("sub", "kc-1"),
            new Claim("name", "Ana Souza"),
            new Claim("preferred_username", "ana"),
            new Claim("email", "ana@exemplo.local"),
            new Claim(ClaimTypes.Role, Papeis.Admin),
            new Claim(ClaimTypes.Role, Papeis.Admin));

        Assert.Equal("kc-1", usuario.KeycloakId);
        Assert.Equal("Ana Souza", usuario.Nome);
        Assert.Equal("ana@exemplo.local", usuario.Email);
        Assert.Equal([Papeis.Admin], usuario.Papeis);
        Assert.True(usuario.EhAdmin);
    }

    [Fact]
    public void Sem_nome_completo_usa_o_login_e_depois_o_id()
    {
        var comLogin = UsuarioDaRequisicao(new Claim("sub", "kc-2"), new Claim("preferred_username", "bruno"));
        var soId = UsuarioDaRequisicao(new Claim("sub", "kc-3"), new Claim(ClaimTypes.Role, Papeis.ClienteExterno));

        Assert.Equal("bruno", comLogin.Nome);
        Assert.Equal("kc-3", soId.Nome);
        Assert.Null(soId.Email);
        Assert.False(soId.EhAdmin);
    }

    [Fact]
    public void Token_sem_sub_e_erro()
    {
        var usuario = UsuarioDaRequisicao(new Claim("name", "Sem id"));

        Assert.Throws<InvalidOperationException>(() => usuario.KeycloakId);
    }

    [Fact]
    public void Fora_de_uma_requisicao_HTTP_e_erro()
    {
        var usuario = new UsuarioAtualHttp(Mock.Of<IHttpContextAccessor>());

        Assert.Throws<InvalidOperationException>(() => usuario.KeycloakId);
    }

    private static UsuarioAtualHttp UsuarioDaRequisicao(params Claim[] claims)
    {
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer", "preferred_username", ClaimTypes.Role)),
        };
        return new UsuarioAtualHttp(Mock.Of<IHttpContextAccessor>(a => a.HttpContext == contexto));
    }
}
