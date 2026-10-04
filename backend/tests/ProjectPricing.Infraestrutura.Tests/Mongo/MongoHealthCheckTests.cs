using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using ProjectPricing.Infraestrutura.Mongo;

namespace ProjectPricing.Infraestrutura.Tests.Mongo;

public class MongoHealthCheckTests
{
    [Fact]
    public async Task Saudavel_quando_o_ping_responde()
    {
        var banco = new Mock<IMongoDatabase>();
        banco
            .Setup(b => b.RunCommandAsync(It.IsAny<Command<BsonDocument>>(), It.IsAny<ReadPreference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocument("ok", 1));

        var resultado = await new MongoHealthCheck(banco.Object).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, resultado.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Nao_saudavel_quando_o_MongoDB_falha_ou_demora(bool falhaDoDriver)
    {
        Exception erro = falhaDoDriver ? new MongoException("sem conexão") : new TimeoutException();
        var banco = new Mock<IMongoDatabase>();
        banco
            .Setup(b => b.RunCommandAsync(It.IsAny<Command<BsonDocument>>(), It.IsAny<ReadPreference>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(erro);

        var resultado = await new MongoHealthCheck(banco.Object).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, resultado.Status);
        Assert.Same(erro, resultado.Exception);
    }
}
