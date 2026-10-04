var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Publicada em /openapi/v1.json em todos os ambientes (standards.md §5).
app.MapOpenApi();
app.MapHealthChecks("/health");

app.Run();
