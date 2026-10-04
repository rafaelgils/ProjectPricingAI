namespace ProjectPricing.Dominio.Conversas;

public sealed class Mensagem
{
    public Mensagem(PapelMensagem papel, string conteudo, DateTimeOffset enviadaEm)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        Papel = papel;
        Conteudo = conteudo;
        EnviadaEm = enviadaEm;
    }

    // Usado pelo driver do MongoDB na leitura.
    private Mensagem()
    {
        Conteudo = string.Empty;
    }

    public PapelMensagem Papel { get; private set; }

    public string Conteudo { get; private set; }

    public DateTimeOffset EnviadaEm { get; private set; }
}
