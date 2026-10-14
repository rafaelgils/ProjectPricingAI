using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectPricing.Api.Limites;

namespace ProjectPricing.Api.Tests.Limites;

public class LimitesDeUsoTests
{
    [Theory]
    [InlineData("POST", "/api/v1/projetos", true)]
    [InlineData("POST", "/api/v1/projetos/", true)]
    [InlineData("POST", "/api/v1/projetos/6703f1c2a9/mensagens", true)]
    [InlineData("POST", "/API/V1/PROJETOS/6703F1C2A9/MENSAGENS", true)]
    [InlineData("GET", "/api/v1/projetos", false)]
    [InlineData("GET", "/api/v1/projetos/6703f1c2a9/mensagens", false)]
    [InlineData("DELETE", "/api/v1/projetos/6703f1c2a9", false)]
    [InlineData("POST", "/api/v1/materiais", false)]
    [InlineData("POST", "/api/v1/projetos/6703f1c2a9", false)]
    public void So_os_posts_que_acionam_o_agente_contam_como_cotacao(string metodo, string caminho, bool esperado)
    {
        var requisicao = new DefaultHttpContext().Request;
        requisicao.Method = metodo;
        requisicao.Path = caminho;

        Assert.Equal(esperado, LimitesDeUso.AcionaOAgente(requisicao));
    }

    [Fact]
    public void Valores_padrao_protegem_o_custo_do_llm()
    {
        var opcoes = new OpcoesLimitesDeUso();

        Assert.Equal(10, opcoes.CotacoesPorMinutoPorUsuario);
        Assert.Equal(300, opcoes.CotacoesPorHoraPorTenant);
        Assert.Equal(120, opcoes.RequisicoesPorMinutoPorUsuario);
    }

    [Fact]
    public void Alem_do_limite_por_usuario_as_requisicoes_sao_recusadas()
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AdicionarLimitesDeUso(new OpcoesLimitesDeUso { RequisicoesPorMinutoPorUsuario = 2 });
        var provedor = servicos.BuildServiceProvider().GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var limitador = provedor.GlobalLimiter!;
        using var primeira = limitador.AttemptAcquire(Contexto("u1"));
        using var segunda = limitador.AttemptAcquire(Contexto("u1"));
        using var terceira = limitador.AttemptAcquire(Contexto("u1"));
        using var outroUsuario = limitador.AttemptAcquire(Contexto("u2"));

        Assert.True(primeira.IsAcquired);
        Assert.True(segunda.IsAcquired);
        Assert.False(terceira.IsAcquired);
        Assert.True(outroUsuario.IsAcquired);
    }

    [Fact]
    public void Teto_por_tenant_vale_para_todos_os_usuarios_da_empresa()
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AdicionarLimitesDeUso(new OpcoesLimitesDeUso { CotacoesPorHoraPorTenant = 2 });
        var opcoes = servicos.BuildServiceProvider().GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var limitador = opcoes.GlobalLimiter!;
        using var a = limitador.AttemptAcquire(Contexto("u1", "empresa-a", cotacao: true));
        using var b = limitador.AttemptAcquire(Contexto("u2", "empresa-a", cotacao: true));
        using var c = limitador.AttemptAcquire(Contexto("u3", "empresa-a", cotacao: true));
        using var outraEmpresa = limitador.AttemptAcquire(Contexto("u4", "empresa-b", cotacao: true));
        using var consultaDaMesmaEmpresa = limitador.AttemptAcquire(Contexto("u5", "empresa-a", cotacao: false));

        Assert.True(a.IsAcquired);
        Assert.True(b.IsAcquired);
        Assert.False(c.IsAcquired);
        Assert.True(outraEmpresa.IsAcquired);
        Assert.True(consultaDaMesmaEmpresa.IsAcquired);
    }

    private static DefaultHttpContext Contexto(string usuario, string tenant = "empresa-a", bool cotacao = false)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", usuario), new Claim("tenant_id", tenant)], "Bearer")),
        };
        http.Request.Method = cotacao ? "POST" : "GET";
        http.Request.Path = "/api/v1/projetos";
        return http;
    }
}
