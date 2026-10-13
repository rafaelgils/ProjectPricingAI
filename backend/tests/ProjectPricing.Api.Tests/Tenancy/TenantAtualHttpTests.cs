using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using ProjectPricing.Api.Tenancy;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Api.Tests.Tenancy;

public class TenantAtualHttpTests
{
    [Fact]
    public void Le_o_tenant_da_claim_do_token()
    {
        var tenant = Criar(new Claim(TenantAtualHttp.Claim, "empresa-a")).TenantId;

        Assert.Equal("empresa-a", tenant);
    }

    [Fact]
    public void Token_sem_a_claim_de_tenant_e_recusado()
    {
        Assert.Throws<TenantNaoIdentificadoException>(() => Criar(new Claim("sub", "u1")).TenantId);
    }

    [Fact]
    public void Claim_de_tenant_vazia_e_recusada()
    {
        Assert.Throws<TenantNaoIdentificadoException>(() => Criar(new Claim(TenantAtualHttp.Claim, "")).TenantId);
    }

    [Fact]
    public void Sem_requisicao_http_nao_ha_tenant()
    {
        var acessor = new Mock<IHttpContextAccessor>();

        Assert.Throws<TenantNaoIdentificadoException>(() => new TenantAtualHttp(acessor.Object).TenantId);
    }

    private static TenantAtualHttp Criar(params Claim[] claims)
    {
        var contexto = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
        return new TenantAtualHttp(new HttpContextAccessor { HttpContext = contexto });
    }
}
