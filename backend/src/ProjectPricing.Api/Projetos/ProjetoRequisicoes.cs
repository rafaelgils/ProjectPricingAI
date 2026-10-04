using FluentValidation;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Api.Projetos;

/// <summary>Corpo do POST /api/v1/projetos.</summary>
public sealed record CriarProjetoRequisicao(string? Descricao);

/// <summary>Corpo do POST /api/v1/projetos/{id}/mensagens.</summary>
public sealed record EnviarMensagemRequisicao(string? Conteudo);

/// <summary>Query string do GET /api/v1/projetos (plano, P4).</summary>
/// <param name="Status">Código do status: rascunho, cotado ou arquivado.</param>
public sealed record ConsultaProjetos(string? Status, int? Pagina, int? Tamanho)
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    public StatusProjeto? StatusFiltro =>
        CodigosEnum<StatusProjeto>.TentarConverter(Status, out var status) ? status : null;

    public int PaginaFiltro => Pagina ?? 1;

    public int TamanhoFiltro => Tamanho ?? TamanhoPadrao;
}

public static class LimitesTexto
{
    /// <summary>Descrição ou mensagem do cliente: o bastante para descrever um projeto com vários itens.</summary>
    public const int MaximoMensagem = 4000;
}

public sealed class CriarProjetoValidador : AbstractValidator<CriarProjetoRequisicao>
{
    public CriarProjetoValidador()
    {
        RuleFor(r => r.Descricao)
            .NotEmpty().WithMessage("Descreva o projeto.")
            .MaximumLength(LimitesTexto.MaximoMensagem)
            .WithMessage($"A descrição tem no máximo {LimitesTexto.MaximoMensagem} caracteres.");
    }
}

public sealed class EnviarMensagemValidador : AbstractValidator<EnviarMensagemRequisicao>
{
    public EnviarMensagemValidador()
    {
        RuleFor(r => r.Conteudo)
            .NotEmpty().WithMessage("Escreva a mensagem.")
            .MaximumLength(LimitesTexto.MaximoMensagem)
            .WithMessage($"A mensagem tem no máximo {LimitesTexto.MaximoMensagem} caracteres.");
    }
}

public sealed class ConsultaProjetosValidador : AbstractValidator<ConsultaProjetos>
{
    public ConsultaProjetosValidador()
    {
        RuleFor(c => c.Status)
            .Must(status => CodigosEnum<StatusProjeto>.TentarConverter(status, out _))
            .When(c => c.Status is not null)
            .WithMessage("Status inválido: use rascunho, cotado ou arquivado.");

        RuleFor(c => c.Pagina)
            .GreaterThanOrEqualTo(1).When(c => c.Pagina is not null)
            .WithMessage("A página começa em 1.");

        RuleFor(c => c.Tamanho)
            .InclusiveBetween(1, ConsultaProjetos.TamanhoMaximo).When(c => c.Tamanho is not null)
            .WithMessage($"O tamanho da página vai de 1 a {ConsultaProjetos.TamanhoMaximo}.");
    }
}
