using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Api.Erros;

/// <summary>
/// Códigos de erro que não vêm de exceções de domínio (standards.md §5).
/// Os de regra de negócio ficam em cada exceção (ex.: <see cref="ItensNaoEncontradosException.CodigoErro"/>).
/// </summary>
public static class CodigosErro
{
    public const string Validacao = "VALIDACAO";
    public const string NaoAutenticado = "NAO_AUTENTICADO";
    public const string AcessoNegado = "ACESSO_NEGADO";
    public const string ErroInterno = "ERRO_INTERNO";

    /// <summary>Código padrão para respostas de erro geradas pelo próprio ASP.NET (ex.: 401 do JwtBearer).</summary>
    public static string? PorStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => Validacao,
        StatusCodes.Status401Unauthorized => NaoAutenticado,
        StatusCodes.Status403Forbidden => AcessoNegado,
        StatusCodes.Status404NotFound => RecursoNaoEncontradoException.CodigoErro,
        StatusCodes.Status500InternalServerError => ErroInterno,
        _ => null,
    };

    /// <summary>Título em português para as respostas de erro geradas pelo próprio ASP.NET.</summary>
    public static string? TituloPorStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Requisição inválida.",
        StatusCodes.Status401Unauthorized => "Não autenticado.",
        StatusCodes.Status403Forbidden => "Acesso negado.",
        StatusCodes.Status404NotFound => "Recurso não encontrado.",
        StatusCodes.Status500InternalServerError => "Erro interno no servidor.",
        _ => null,
    };

    /// <summary>URI do campo "type" (ex.: ITENS_NAO_ENCONTRADOS → https://precificacao/erros/itens-nao-encontrados).</summary>
    public static string Tipo(string codigo) =>
        "https://precificacao/erros/" + codigo.ToLowerInvariant().Replace('_', '-');
}
