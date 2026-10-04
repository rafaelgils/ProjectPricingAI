namespace ProjectPricing.Dominio.Excecoes;

/// <summary>
/// O projeto mudou entre a leitura e a gravação (ex.: dois refinamentos ao mesmo tempo). A segunda alteração
/// é recusada para não sobrescrever a primeira.
/// </summary>
public sealed class ConflitoDeEdicaoException : ExcecaoDeDominio
{
    public const string CodigoErro = "CONFLITO_EDICAO";

    public ConflitoDeEdicaoException()
        : base(CodigoErro, "O projeto foi alterado por outra mensagem. Recarregue o projeto e tente de novo.")
    {
    }
}
