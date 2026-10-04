using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ProjectPricing.Infraestrutura.Mongo;

/// <summary>O backend só fica saudável se alcança o MongoDB (comando ping).</summary>
public sealed class MongoHealthCheck(IMongoDatabase banco) : IHealthCheck
{
    private static readonly BsonDocument ComandoPing = new("ping", 1);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await banco.RunCommandAsync<BsonDocument>(ComandoPing, cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception erro) when (erro is MongoException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("MongoDB indisponível.", erro);
        }
    }
}
