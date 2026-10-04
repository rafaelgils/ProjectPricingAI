using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Projetos;
using ProjectPricing.Infraestrutura.Keycloak;
using ProjectPricing.Infraestrutura.Mongo;
using ProjectPricing.Infraestrutura.Repositorios;

namespace ProjectPricing.Infraestrutura;

public static class InjecaoDeDependencia
{
    /// <param name="stringConexaoMongo">Precisa indicar o banco (ex.: mongodb://usuario:senha@host:27017/precificacao).</param>
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services, string stringConexaoMongo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stringConexaoMongo);

        var url = MongoUrl.Create(stringConexaoMongo);
        if (string.IsNullOrWhiteSpace(url.DatabaseName))
        {
            throw new InvalidOperationException("A string de conexão do MongoDB precisa indicar o banco.");
        }

        MapeamentoMongo.Registrar();

        // MongoClient é thread-safe e deve ser único por processo.
        services.AddSingleton<IMongoClient>(_ => new MongoClient(url));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(url.DatabaseName));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>().GetCollection<Material>(NomesColecoes.Materiais));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>().GetCollection<Projeto>(NomesColecoes.Projetos));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>().GetCollection<Conversa>(NomesColecoes.Conversas));

        services.AddScoped<IMaterialRepository, MaterialRepository>();
        services.AddScoped<IProjetoRepository, ProjetoRepository>();
        services.AddScoped<IConversaRepository, ConversaRepository>();

        return services;
    }

    /// <summary>Cliente da Keycloak Admin API (RF10). Um por processo, para reaproveitar o token.</summary>
    public static IServiceCollection AdicionarKeycloakAdmin(this IServiceCollection services, OpcoesKeycloakAdmin opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        services.AddSingleton(opcoes);
        services.AddHttpClient(nameof(KeycloakAdminClient));
        // O token da service account fica em cache na instância; ela precisa durar o processo inteiro.
        services.AddSingleton<IGestaoIdentidade>(sp => new KeycloakAdminClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(KeycloakAdminClient)),
            opcoes,
            sp.GetRequiredService<TimeProvider>()));

        return services;
    }
}
