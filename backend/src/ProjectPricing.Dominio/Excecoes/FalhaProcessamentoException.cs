namespace ProjectPricing.Dominio.Excecoes;

/// <summary>Falha do agente ou do LLM (RN12). A mensagem ao usuário é sempre a mesma.</summary>
public sealed class FalhaProcessamentoException : ExcecaoDeDominio
{
    public const string CodigoErro = "FALHA_PROCESSAMENTO";

    public const string MensagemFixa =
        "Não foi possível processar essa mensagem no momento, favor contate o administrador";

    public FalhaProcessamentoException(Exception? causa = null)
        : base(CodigoErro, MensagemFixa)
    {
        Causa = causa;
    }

    /// <summary>Erro original, só para log; nunca vai para a resposta.</summary>
    public Exception? Causa { get; }
}
