using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Aplicacao.Materiais;

/// <summary>Dados de cadastro e alteração de material, já validados na borda da API.</summary>
public sealed record DadosMaterial(
    string Nome,
    IReadOnlyList<string> Sinonimos,
    TipoMaterial Tipo,
    string Categoria,
    UnidadeMedida Unidade,
    decimal PrecoUnitario,
    string Fornecedor);
