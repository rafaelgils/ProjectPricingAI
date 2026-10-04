namespace ProjectPricing.Dominio.Conversas;

/// <summary>Histórico de mensagens entre cliente e agente de um projeto (relação 1:1).</summary>
public sealed class Conversa
{
    public Conversa(string projetoId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projetoId);

        ProjetoId = projetoId;
    }

    // Usado pelo driver do MongoDB na leitura.
    private Conversa()
    {
        ProjetoId = string.Empty;
    }

    public string? Id { get; private set; }

    public string ProjetoId { get; private set; }

    public IReadOnlyList<Mensagem> Mensagens { get; private set; } = [];

    public void AdicionarMensagem(Mensagem mensagem)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        Mensagens = [.. Mensagens, mensagem];
    }
}
