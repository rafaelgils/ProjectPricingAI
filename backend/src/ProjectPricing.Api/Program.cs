using System.Text.Json.Serialization;
using FluentValidation;
using ProjectPricing.Api.Autenticacao;
using ProjectPricing.Api.Autorizacao;
using ProjectPricing.Api.Erros;
using ProjectPricing.Api.Json;
using ProjectPricing.Api.Materiais;
using ProjectPricing.Api.Usuarios;
using ProjectPricing.Api.Validacao;
using ProjectPricing.Aplicacao.Materiais;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Infraestrutura;
using ProjectPricing.Infraestrutura.Mongo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
var fuso = TimeZoneInfo.FindSystemTimeZoneById(
    builder.Configuration["FusoHorario"] ?? ConversorDataComFuso.FusoPadrao);
builder.Services.ConfigureHttpJsonOptions(json =>
{
    // Enums com os códigos da documentação (ex.: "m2", "cotado") e datas com o fuso -03:00.
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    json.SerializerOptions.Converters.Add(new ConversorDataComFuso(fuso));
});

builder.Services.AddProblemDetails(opcoes =>
    opcoes.CustomizeProblemDetails = contexto => MapeadorDeErros.CompletarCodigo(contexto.ProblemDetails));
builder.Services.AddExceptionHandler<TratadorDeExcecoes>();

ConfiguracaoValidacao.Aplicar();
builder.Services.AddSingleton<IValidator<CriarMaterialRequisicao>, CriarMaterialValidador>();
builder.Services.AddSingleton<IValidator<AlterarMaterialRequisicao>, AlterarMaterialValidador>();
builder.Services.AddSingleton<IValidator<ConsultaMateriais>, ConsultaMateriaisValidador>();

builder.Services.AdicionarAutenticacaoKeycloak(builder.Configuration);
builder.Services.AdicionarPoliticasDeAutorizacao();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ServicoCatalogo>();

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
    .MapearUsuarios()
    .MapearMateriais();

app.Run();
