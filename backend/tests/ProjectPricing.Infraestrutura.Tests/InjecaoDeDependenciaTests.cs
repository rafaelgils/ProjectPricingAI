using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Projetos;
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

    [Theory]
    [InlineData("")]
    [InlineData("mongodb://mongodb:27017")]
    public void String_de_conexao_precisa_indicar_o_banco(string stringConexao)
    {
        Assert.ThrowsAny<Exception>(() => new ServiceCollection().AdicionarInfraestrutura(stringConexao));
    }
}
