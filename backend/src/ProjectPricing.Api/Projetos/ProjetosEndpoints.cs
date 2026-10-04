using Microsoft.AspNetCore.Http.HttpResults;
using ProjectPricing.Api.Autorizacao;
using ProjectPricing.Api.Materiais;
using ProjectPricing.Api.Sse;
using ProjectPricing.Api.Validacao;
using ProjectPricing.Aplicacao.Projetos;

namespace ProjectPricing.Api.Projetos;

/// <summary>
/// Projetos e conversas (architecture.md §4). Não existe PUT: o projeto só muda pela conversa com o agente (RN02).
/// O dono e o Admin são verificados no serviço (RN07); a policy só exige um dos três papéis.
/// </summary>
public static class ProjetosEndpoints
{
    public const string Caminho = "/api/v1/projetos";

    public static RouteGroupBuilder MapearProjetos(this RouteGroupBuilder api)
    {
        var projetos = api.MapGroup("/projetos").WithTags("Projetos").RequireAuthorization(Politicas.PodeCotar);

        projetos.MapPost("/", Criar)
            .Validar<CriarProjetoRequisicao>()
            .Produces(StatusCodes.Status201Created, contentType: EscritorSse.TipoConteudo)
            .WithSummary("Cria o projeto a partir da descrição e responde a cotação em SSE");

        projetos.MapGet("/", Listar)
            .Validar<ConsultaProjetos>()
            .WithSummary("Lista os projetos: os próprios para clientes, todos para o Admin");

        projetos.MapGet("/{id}", Obter)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Detalhe do projeto com os itens");

        projetos.MapDelete("/{id}", Arquivar)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Arquiva o projeto (exclusão lógica)");

        projetos.MapPost("/{id}/mensagens", EnviarMensagem)
            .Validar<EnviarMensagemRequisicao>()
            .Produces(StatusCodes.Status200OK, contentType: EscritorSse.TipoConteudo)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Mensagem de refinamento; resposta em SSE");

        projetos.MapGet("/{id}/mensagens", Historico)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Histórico da conversa");

        return api;
    }

    public static async Task<EmptyHttpResult> Criar(
        CriarProjetoRequisicao? requisicao,
        ServicoProjetos servico,
        CondutorConversa condutor,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        // O FiltroValidacao já recusou a descrição ausente (400 antes do stream).
        var descricao = requisicao!.Descricao!.Trim();
        var rodada = await servico.CriarAsync(descricao, cancellationToken);

        await condutor.ResponderAsync(
            http.Response, StatusCodes.Status201Created, $"{Caminho}/{rodada.Projeto.Id}",
            rodada, descricao, CondutorConversa.AvisoDescricao, cancellationToken);
        return TypedResults.Empty;
    }

    public static async Task<EmptyHttpResult> EnviarMensagem(
        string id,
        EnviarMensagemRequisicao? requisicao,
        ServicoProjetos servico,
        CondutorConversa condutor,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        // 404 (outro cliente) e 422 (arquivado, RN11) saem como resposta HTTP comum, antes do stream.
        var rodada = await servico.PrepararMensagemAsync(id, cancellationToken);

        await condutor.ResponderAsync(
            http.Response, StatusCodes.Status200OK, location: null,
            rodada, requisicao!.Conteudo!.Trim(), CondutorConversa.AvisoMensagem, cancellationToken);
        return TypedResults.Empty;
    }

    public static async Task<Ok<PaginaResposta<ProjetoResumoResposta>>> Listar(
        [AsParameters] ConsultaProjetos consulta,
        ServicoProjetos servico,
        CancellationToken cancellationToken)
    {
        var pagina = await servico.ListarAsync(consulta.StatusFiltro, consulta.PaginaFiltro, consulta.TamanhoFiltro, cancellationToken);

        return TypedResults.Ok(new PaginaResposta<ProjetoResumoResposta>(
            [.. pagina.Itens.Select(ProjetoResumoResposta.De)], consulta.PaginaFiltro, consulta.TamanhoFiltro, pagina.Total));
    }

    public static async Task<Ok<ProjetoResposta>> Obter(
        string id,
        ServicoProjetos servico,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(ProjetoResposta.De(await servico.ObterAsync(id, cancellationToken)));

    public static async Task<NoContent> Arquivar(
        string id,
        ServicoProjetos servico,
        CancellationToken cancellationToken)
    {
        await servico.ArquivarAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<Ok<IReadOnlyList<MensagemResposta>>> Historico(
        string id,
        ServicoProjetos servico,
        CancellationToken cancellationToken)
    {
        var mensagens = await servico.HistoricoAsync(id, cancellationToken);
        return TypedResults.Ok<IReadOnlyList<MensagemResposta>>([.. mensagens.Select(MensagemResposta.De)]);
    }
}
