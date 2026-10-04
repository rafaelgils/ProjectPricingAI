namespace ProjectPricing.Dominio.Excecoes;

/// <summary>Nome ou sinônimo já usado por outro material, inclusive inativo (RN10).</summary>
public sealed class MaterialDuplicadoException : ExcecaoDeDominio
{
    public const string CodigoErro = "MATERIAL_DUPLICADO";

    public MaterialDuplicadoException(string termoDuplicado)
        : base(CodigoErro, $"Já existe um material com o nome ou sinônimo \"{termoDuplicado}\".")
    {
        TermoDuplicado = termoDuplicado;
    }

    public string TermoDuplicado { get; }
}
