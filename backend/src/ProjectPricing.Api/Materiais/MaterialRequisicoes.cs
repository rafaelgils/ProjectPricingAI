using ProjectPricing.Aplicacao.Materiais;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Api.Materiais;

/// <summary>Corpo do POST /api/v1/materiais. Enums anuláveis para o validador apontar o campo ausente.</summary>
public record CriarMaterialRequisicao(
    string? Nome,
    IReadOnlyList<string>? Sinonimos,
    TipoMaterial? Tipo,
    string? Categoria,
    UnidadeMedida? Unidade,
    decimal? PrecoUnitario,
    string? Fornecedor)
{
    /// <summary>Só chamado depois da validação, que garante os campos obrigatórios.</summary>
    public DadosMaterial ParaDados() => new(
        Nome!,
        Sinonimos ?? [],
        Tipo!.Value,
        Categoria!,
        Unidade!.Value,
        PrecoUnitario!.Value,
        Fornecedor!);
}

/// <summary>Corpo do PUT /api/v1/materiais/{id}: os mesmos campos, mais o status (inativar ou reativar).</summary>
public sealed record AlterarMaterialRequisicao(
    string? Nome,
    IReadOnlyList<string>? Sinonimos,
    TipoMaterial? Tipo,
    string? Categoria,
    UnidadeMedida? Unidade,
    decimal? PrecoUnitario,
    string? Fornecedor,
    StatusMaterial? Status)
    : CriarMaterialRequisicao(Nome, Sinonimos, Tipo, Categoria, Unidade, PrecoUnitario, Fornecedor);

/// <summary>Query string do GET /api/v1/materiais (plano, P4: tamanho padrão 20, máximo 100).</summary>
/// <param name="Status">Código do status ("ativo" ou "inativo"), validado pelo <see cref="ConsultaMateriaisValidador"/>.</param>
public sealed record ConsultaMateriais(
    string? Busca,
    string? Categoria,
    string? Status,
    int? Pagina,
    int? Tamanho)
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    public FiltroMateriais ParaFiltro()
    {
        StatusMaterial? status = CodigosEnum<StatusMaterial>.TentarConverter(Status, out var valor) ? valor : null;
        return new FiltroMateriais(Busca, Categoria, status, Pagina ?? 1, Tamanho ?? TamanhoPadrao);
    }
}
