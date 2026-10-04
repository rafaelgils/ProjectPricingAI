using Microsoft.AspNetCore.Http.HttpResults;
using ProjectPricing.Api.Autorizacao;
using ProjectPricing.Api.Validacao;
using ProjectPricing.Aplicacao.Materiais;

namespace ProjectPricing.Api.Materiais;

/// <summary>Catálogo de materiais (architecture.md §4). O endpoint só traduz HTTP; as regras ficam no serviço.</summary>
public static class MateriaisEndpoints
{
    public const string Caminho = "/api/v1/materiais";

    public static RouteGroupBuilder MapearMateriais(this RouteGroupBuilder api)
    {
        var materiais = api.MapGroup("/materiais").WithTags("Materiais");

        materiais.MapGet("/", Listar)
            .RequireAuthorization(Politicas.PodeConsultarCatalogo)
            .Validar<ConsultaMateriais>()
            .WithSummary("Lista paginada do catálogo; clientes veem só os materiais ativos");

        materiais.MapGet("/{id}", Obter)
            .RequireAuthorization(Politicas.PodeConsultarCatalogo)
            .WithSummary("Detalhe do material");

        materiais.MapPost("/", Criar)
            .RequireAuthorization(Politicas.PodeGerenciarCatalogo)
            .Validar<CriarMaterialRequisicao>()
            .WithSummary("Cadastra material ou serviço");

        materiais.MapPut("/{id}", Alterar)
            .RequireAuthorization(Politicas.PodeGerenciarCatalogo)
            .Validar<AlterarMaterialRequisicao>()
            .WithSummary("Altera ou reativa o material; não afeta cotações emitidas");

        materiais.MapDelete("/{id}", Inativar)
            .RequireAuthorization(Politicas.PodeGerenciarCatalogo)
            .WithSummary("Inativa o material (exclusão lógica)");

        return api;
    }

    public static async Task<Ok<PaginaResposta<MaterialResposta>>> Listar(
        [AsParameters] ConsultaMateriais consulta,
        ServicoCatalogo servico,
        CancellationToken cancellationToken)
    {
        var filtro = consulta.ParaFiltro();
        var pagina = await servico.ListarAsync(filtro, cancellationToken);

        return TypedResults.Ok(new PaginaResposta<MaterialResposta>(
            [.. pagina.Itens.Select(MaterialResposta.De)], filtro.Pagina, filtro.Tamanho, pagina.Total));
    }

    public static async Task<Ok<MaterialResposta>> Obter(
        string id,
        ServicoCatalogo servico,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(MaterialResposta.De(await servico.ObterAsync(id, cancellationToken)));
    }

    public static async Task<Created<MaterialResposta>> Criar(
        CriarMaterialRequisicao? requisicao,
        ServicoCatalogo servico,
        CancellationToken cancellationToken)
    {
        // O FiltroValidacao já recusou o corpo ausente.
        var material = await servico.CriarAsync(requisicao!.ParaDados(), cancellationToken);
        var resposta = MaterialResposta.De(material);

        return TypedResults.Created($"{Caminho}/{resposta.Id}", resposta);
    }

    public static async Task<Ok<MaterialResposta>> Alterar(
        string id,
        AlterarMaterialRequisicao? requisicao,
        ServicoCatalogo servico,
        CancellationToken cancellationToken)
    {
        var material = await servico.AlterarAsync(
            id, requisicao!.ParaDados(), requisicao.Status!.Value, cancellationToken);

        return TypedResults.Ok(MaterialResposta.De(material));
    }

    public static async Task<NoContent> Inativar(
        string id,
        ServicoCatalogo servico,
        CancellationToken cancellationToken)
    {
        await servico.InativarAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
