using System.Text.Json.Serialization;
using FluentValidation;
using ProjectPricing.Api.Autenticacao;
using ProjectPricing.Api.Autorizacao;
using ProjectPricing.Api.Erros;
using ProjectPricing.Api.Json;
using ProjectPricing.Api.Materiais;
using ProjectPricing.Api.Mcp;
using ProjectPricing.Api.Projetos;
using ProjectPricing.Api.Usuarios;
using ProjectPricing.Api.Validacao;
using ProjectPricing.Aplicacao.Agente;
using ProjectPricing.Aplicacao.Cotacao;
using ProjectPricing.Aplicacao.Materiais;
using ProjectPricing.Aplicacao.Projetos;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Precificacao;
using ProjectPricing.Infraestrutura;
using ProjectPricing.Infraestrutura.Keycloak;
using ProjectPricing.Infraestrutura.Llm;
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
builder.Services.AddSingleton<IValidator<CriarProjetoRequisicao>, CriarProjetoValidador>();
builder.Services.AddSingleton<IValidator<EnviarMensagemRequisicao>, EnviarMensagemValidador>();
builder.Services.AddSingleton<IValidator<ConsultaProjetos>, ConsultaProjetosValidador>();
builder.Services.AddSingleton<IValidator<CriarUsuarioRequisicao>, CriarUsuarioValidador>();
builder.Services.AddSingleton<IValidator<AlterarUsuarioRequisicao>, AlterarUsuarioValidador>();
builder.Services.AddSingleton<IValidator<ConsultaUsuarios>, ConsultaUsuariosValidador>();

builder.Services.AdicionarAutenticacaoKeycloak(builder.Configuration);
builder.Services.AdicionarPoliticasDeAutorizacao();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();
builder.Services.AddScoped<ServicoUsuarios>();
// Gestão de usuários (RF10): repasse à Keycloak Admin API com a service account precificacao-admin.
builder.Services.AdicionarKeycloakAdmin(new OpcoesKeycloakAdmin
{
    UrlRealm = builder.Configuration["Keycloak:UrlInterna"] is { Length: > 0 } urlInterna
        ? urlInterna
        : builder.Configuration["Keycloak:UrlPublica"] ?? string.Empty,
    ClientId = builder.Configuration["Keycloak:AdminClientId"] ?? "precificacao-admin",
    ClientSecret = builder.Configuration["Keycloak:AdminClientSecret"],
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ServicoCatalogo>();

// Precificação determinística (ADR-001), tools do agente (ADR-006) e Agente de Projetos.
builder.Services.AddSingleton<IConversorUnidades>(ConversorUnidades.CriarPadrao());
builder.Services.AddSingleton<IServicoPrecificacao, ServicoPrecificacao>();
builder.Services.AddScoped<IFerramentasCotacao, FerramentasCotacao>();
builder.Services.AddScoped<IAgenteProjetos, AgenteProjetos>();
builder.Services.AddScoped<ServicoProjetos>();
builder.Services.AddScoped<CondutorConversa>();
builder.Services.AdicionarClaude(
    builder.Configuration.GetSection(OpcoesAnthropic.Secao).Get<OpcoesAnthropic>() ?? new OpcoesAnthropic());
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<FerramentasMcp>();

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
// Servidor MCP só na rede interna: o Kong não roteia /mcp (ADR-006).
app.MapMcp("/mcp").RequireAuthorization(Politicas.PodeCotar);

// Erros comuns a todas as rotas, em Problem Details (standards.md §5).
app.MapGroup("/api/v1")
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .MapearUsuarios()
    .MapearMateriais()
    .MapearProjetos();

app.Run();
