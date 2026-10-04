namespace ProjectPricing.Dominio.Excecoes;

/// <summary>
/// Erro de regra de negócio. O <see cref="Codigo"/> é o campo <c>code</c> estável do Problem Details
/// (standards.md §5); o status HTTP é decidido pela API.
/// </summary>
public abstract class ExcecaoDeDominio : Exception
{
    protected ExcecaoDeDominio(string codigo, string mensagem)
        : base(mensagem)
    {
        Codigo = codigo;
    }

    public string Codigo { get; }
}
