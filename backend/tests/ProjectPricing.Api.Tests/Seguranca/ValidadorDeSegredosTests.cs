using Microsoft.Extensions.Configuration;
using ProjectPricing.Api.Seguranca;

namespace ProjectPricing.Api.Tests.Seguranca;

public class ValidadorDeSegredosTests
{
    [Fact]
    public void Segredos_reais_passam()
    {
        var configuracao = Configuracao(
            ("ConnectionStrings:MongoDB", "mongodb://app:Xk29!pQ@mongodb:27017/precificacao"),
            ("Keycloak:AdminClientSecret", "b8c1d0f2-7a3e"));

        ValidadorDeSegredos.Validar(configuracao);
    }

    [Fact]
    public void Segredos_ausentes_nao_sao_tratados_como_exemplo()
    {
        ValidadorDeSegredos.Validar(Configuracao());
    }

    [Fact]
    public void Valor_de_exemplo_derruba_a_subida_e_aponta_a_chave()
    {
        var configuracao = Configuracao(
            ("ConnectionStrings:MongoDB", "mongodb://app:troque-esta-senha@mongodb:27017/precificacao"),
            ("Keycloak:AdminClientSecret", "TROQUE-ESTE-SEGREDO"),
            ("Anthropic:ApiKey", "sk-ant-real"));

        var erro = Assert.Throws<InvalidOperationException>(() => ValidadorDeSegredos.Validar(configuracao));

        Assert.Contains("ConnectionStrings:MongoDB", erro.Message, StringComparison.Ordinal);
        Assert.Contains("Keycloak:AdminClientSecret", erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Anthropic:ApiKey", erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("esta-senha", erro.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuracao(params (string Chave, string Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Chave, v.Valor)))
            .Build();
}
