using System.Text.Json.Serialization;
using ProjectPricing.Api.Autenticacao;
using ProjectPricing.Api.Autorizacao;
using ProjectPricing.Api.Erros;
using ProjectPricing.Api.Usuarios;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Infraestrutura;
using ProjectPricing.Infraestrutura.Mongo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
// Enums em JSON com os códigos da documentação (ex.: "m2", "cotado").
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails(opcoes =>
    opcoes.CustomizeProblemDetails = contexto => MapeadorDeErros.CompletarCodigo(contexto.ProblemDetails));
builder.Services.AddExceptionHandler<TratadorDeExcecoes>();

builder.Services.AdicionarAutenticacaoKeycloak(builder.Configuration);
builder.Services.AdicionarPoliticasDeAutorizacao();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();

var stringConexaoMongo = builder.Configuration.GetConnectionString("MongoDB")
    ?? throw new InvalidOperationException("ConnectionStrings:MongoDB não configurada.");
builder.Services.AdicionarInfraestrutura(stringConexaoMongo);
builder.Services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");

var app = builder.Build();

app.UseExceptionHandler();
// 401, 403 e 404 sem corpo também saem em Problem Details.
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

// Publicada em /openapi/v1.json em todos os ambientes (standards.md §5).
app.MapOpenApi().AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

app.MapGroup("/api/v1")
    .MapearUsuarios();

app.Run();
