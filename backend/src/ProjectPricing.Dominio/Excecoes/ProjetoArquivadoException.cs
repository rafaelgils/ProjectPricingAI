namespace ProjectPricing.Dominio.Excecoes;

/// <summary>Projeto arquivado é somente leitura (RN11).</summary>
public sealed class ProjetoArquivadoException : ExcecaoDeDominio
{
    public const string CodigoErro = "PROJETO_ARQUIVADO";

    public ProjetoArquivadoException()
        : base(CodigoErro, "Este projeto está inativo.")
    {
    }
}
