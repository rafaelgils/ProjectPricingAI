using FluentValidation;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Api.Materiais;

/// <summary>Validação de entrada do cadastro (RN10). Duplicidade com outros materiais é regra do serviço.</summary>
public class CriarMaterialValidador : AbstractValidator<CriarMaterialRequisicao>
{
    public const int TamanhoMaximoNome = 150;
    public const int TamanhoMaximoCategoria = 100;
    public const int MaximoSinonimos = 20;
    private const int CasasDecimaisPreco = 2;
    private const int DigitosPreco = 14;

    public CriarMaterialValidador()
    {
        RuleFor(r => r.Nome)
            .NotEmpty().WithMessage("Informe o nome.")
            .MaximumLength(TamanhoMaximoNome).WithMessage($"O nome tem no máximo {TamanhoMaximoNome} caracteres.");

        RuleFor(r => r.Sinonimos)
            .Must(s => s is null || s.Count <= MaximoSinonimos)
            .WithMessage($"Informe no máximo {MaximoSinonimos} sinônimos.")
            .Must((requisicao, sinonimos) => !TemTermoRepetido(requisicao.Nome, sinonimos))
            .WithMessage("Os sinônimos não podem se repetir nem repetir o nome do material.");
        RuleForEach(r => r.Sinonimos)
            .NotEmpty().WithMessage("Sinônimo não pode ser vazio.")
            .MaximumLength(TamanhoMaximoNome).WithMessage($"Cada sinônimo tem no máximo {TamanhoMaximoNome} caracteres.");

        RuleFor(r => r.Tipo)
            .NotNull().WithMessage("Informe o tipo: material ou servico.")
            .IsInEnum().WithMessage("Tipo inválido.");

        RuleFor(r => r.Categoria)
            .NotEmpty().WithMessage("Informe a categoria.")
            .MaximumLength(TamanhoMaximoCategoria).WithMessage($"A categoria tem no máximo {TamanhoMaximoCategoria} caracteres.");

        RuleFor(r => r.Unidade)
            .NotNull().WithMessage("Informe a unidade: m, m2, un, L ou h.")
            .IsInEnum().WithMessage("Unidade inválida.");

        RuleFor(r => r.PrecoUnitario)
            .NotNull().WithMessage("Informe o preço unitário.")
            .GreaterThan(0).WithMessage("O preço unitário precisa ser maior que zero.")
            .PrecisionScale(DigitosPreco, CasasDecimaisPreco, ignoreTrailingZeros: true)
            .WithMessage($"O preço unitário tem no máximo {CasasDecimaisPreco} casas decimais.");

        RuleFor(r => r.Fornecedor)
            .NotEmpty().WithMessage("Informe o fornecedor.")
            .MaximumLength(TamanhoMaximoNome).WithMessage($"O fornecedor tem no máximo {TamanhoMaximoNome} caracteres.");
    }

    private static bool TemTermoRepetido(string? nome, IReadOnlyList<string>? sinonimos)
    {
        if (sinonimos is null || sinonimos.Count == 0)
        {
            return false;
        }

        var termos = sinonimos.Prepend(nome ?? string.Empty)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(NormalizadorTexto.Normalizar)
            .ToList();
        return termos.Count != termos.Distinct(StringComparer.Ordinal).Count();
    }
}

public sealed class AlterarMaterialValidador : AbstractValidator<AlterarMaterialRequisicao>
{
    public AlterarMaterialValidador()
    {
        Include(new CriarMaterialValidador());

        RuleFor(r => r.Status)
            .NotNull().WithMessage("Informe o status: ativo ou inativo.")
            .IsInEnum().WithMessage("Status inválido.");
    }
}

public sealed class ConsultaMateriaisValidador : AbstractValidator<ConsultaMateriais>
{
    public ConsultaMateriaisValidador()
    {
        RuleFor(c => c.Pagina)
            .GreaterThanOrEqualTo(1).When(c => c.Pagina is not null)
            .WithMessage("A página começa em 1.");

        RuleFor(c => c.Tamanho)
            .InclusiveBetween(1, ConsultaMateriais.TamanhoMaximo).When(c => c.Tamanho is not null)
            .WithMessage($"O tamanho da página vai de 1 a {ConsultaMateriais.TamanhoMaximo}.");

        RuleFor(c => c.Status)
            .Must(status => CodigosEnum<StatusMaterial>.TentarConverter(status, out _))
            .When(c => c.Status is not null)
            .WithMessage("Status inválido: use ativo ou inativo.");

        RuleFor(c => c.Busca)
            .MaximumLength(CriarMaterialValidador.TamanhoMaximoNome)
            .WithMessage($"A busca tem no máximo {CriarMaterialValidador.TamanhoMaximoNome} caracteres.");

        RuleFor(c => c.Categoria)
            .MaximumLength(CriarMaterialValidador.TamanhoMaximoCategoria)
            .WithMessage($"A categoria tem no máximo {CriarMaterialValidador.TamanhoMaximoCategoria} caracteres.");
    }
}
