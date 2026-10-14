using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Projetos;
using ProjectPricing.Aplicacao.Idempotencia;
using ProjectPricing.Infraestrutura.Keycloak;
using ProjectPricing.Infraestrutura.Repositorios;

namespace ProjectPricing.Infraestrutura.Tests;

/// <summary>Só inspeciona os registros; nenhum MongoClient é criado (sem conexão real).</summary>
public class InjecaoDeDependenciaTests
{
    [Fact]
    public void Registra_os_repositorios_e_as_colecoes()
    {
        var services = new ServiceCollection();

        services.AdicionarInfraestrutura("mongodb://usuario:senha@mongodb:27017/precificacao");

        Assert.Contains(services, s => s.ServiceType == typeof(IMaterialRepository) && s.ImplementationType == typeof(MaterialRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IProjetoRepository) && s.ImplementationType == typeof(ProjetoRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IConversaRepository) && s.ImplementationType == typeof(ConversaRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IMongoCollection<Material>));
        Assert.Contains(services, s => s.ServiceType == typeof(IMongoClient) && s.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void Registra_o_armazem_de_idempotencia_por_requisicao()
    {
        var services = new ServiceCollection();

        services.AdicionarInfraestrutura("mongodb://usuario:senha@mongodb:27017/precificacao");

        Assert.Contains(services, s => s.ServiceType == typeof(IArmazemIdempotencia)
            && s.ImplementationType == typeof(ArmazemIdempotenciaMongo)
            && s.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Cliente_do_keycloak_tem_o_pipeline_de_resiliencia()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);

        services.AdicionarKeycloakAdmin(new OpcoesKeycloakAdmin { UrlRealm = "http://keycloak:8080/realms/precificacao" });

        using var provedor = services.BuildServiceProvider();
        var opcoes = provedor.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>().Get($"{nameof(KeycloakAdminClient)}-standard");
        Assert.True(opcoes.Retry.MaxRetryAttempts > 0);
        Assert.True(opcoes.CircuitBreaker.MinimumThroughput > 0);
        Assert.True(opcoes.TotalRequestTimeout.Timeout > TimeSpan.Zero);
    }

    [Theory]
    [InlineData("")]
    [InlineData("mongodb://mongodb:27017")]
    public void String_de_conexao_precisa_indicar_o_banco(string stringConexao)
    {
        Assert.ThrowsAny<Exception>(() => new ServiceCollection().AdicionarInfraestrutura(stringConexao));
    }
}
