using Microsoft.AspNetCore.Mvc;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Api.Erros;

/// <summary>Traduz exceções em Problem Details (RFC 7807) com o campo <c>code</c> (standards.md §5).</summary>
public static class MapeadorDeErros
{
    public const string ExtensaoCodigo = "code";

    public static ProblemDetails Mapear(Exception excecao)
    {
        return excecao switch
        {
            ExcecaoDeDominio dominio => MapearDominio(dominio),
            BadHttpRequestException requisicaoInvalida => Criar(
                StatusCodes.Status400BadRequest, CodigosErro.Validacao, requisicaoInvalida.Message),
            _ => Criar(
                StatusCodes.Status500InternalServerError,
                CodigosErro.ErroInterno,
                CodigosErro.TituloPorStatus(StatusCodes.Status500InternalServerError)!),
        };
    }

    private static ProblemDetails MapearDominio(ExcecaoDeDominio excecao)
    {
        var problema = Criar(StatusDe(excecao), excecao.Codigo, excecao.Message);

        switch (excecao)
        {
            case ItensNaoEncontradosException naoEncontrados:
                problema.Extensions["itensNaoEncontrados"] = naoEncontrados.ItensNaoEncontrados;
                problema.Extensions["projetoId"] = naoEncontrados.ProjetoId;
                break;
            case EsclarecimentoNecessarioException esclarecimento:
                problema.Extensions["pergunta"] = esclarecimento.Pergunta;
                problema.Extensions["sugestoes"] = esclarecimento.Sugestoes;
                problema.Extensions["projetoId"] = esclarecimento.ProjetoId;
                break;
            case MaterialDuplicadoException duplicado:
                problema.Extensions["termoDuplicado"] = duplicado.TermoDuplicado;
                break;
        }

        return problema;
    }

    private static int StatusDe(ExcecaoDeDominio excecao) => excecao switch
    {
        RecursoNaoEncontradoException => StatusCodes.Status404NotFound,
        MaterialDuplicadoException => StatusCodes.Status409Conflict,
        FalhaProcessamentoException => StatusCodes.Status500InternalServerError,
        // ITENS_NAO_ENCONTRADOS, ESCLARECIMENTO_NECESSARIO e PROJETO_ARQUIVADO: regra de negócio violada.
        _ => StatusCodes.Status422UnprocessableEntity,
    };

    private static ProblemDetails Criar(int status, string codigo, string titulo)
    {
        var problema = new ProblemDetails
        {
            Status = status,
            Type = CodigosErro.Tipo(codigo),
            Title = titulo,
        };
        problema.Extensions[ExtensaoCodigo] = codigo;
        return problema;
    }

    /// <summary>
    /// Completa respostas de erro que não passaram pelo mapeador (ex.: 401 e 403 da autenticação),
    /// para que toda resposta de erro tenha <c>code</c> e <c>type</c>.
    /// </summary>
    public static void CompletarCodigo(ProblemDetails problema)
    {
        if (problema.Extensions.ContainsKey(ExtensaoCodigo) || problema.Status is not { } status)
        {
            return;
        }

        var codigo = CodigosErro.PorStatus(status);
        if (codigo is null)
        {
            return;
        }

        problema.Extensions[ExtensaoCodigo] = codigo;
        problema.Type = CodigosErro.Tipo(codigo);
        // Os títulos padrão do ASP.NET vêm em inglês ("Unauthorized"); a API responde em português.
        problema.Title = CodigosErro.TituloPorStatus(status) ?? problema.Title;
    }
}
