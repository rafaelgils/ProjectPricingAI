namespace ProjectPricing.Dominio.Projetos;

/// <summary>Pedido de cotação de um cliente (business-rules.md §6 e §7).</summary>
public sealed class Projeto
{
    public Projeto(string clienteId, string descricao, DateTimeOffset criadoEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(descricao);

        ClienteId = clienteId;
        Descricao = descricao.Trim();
        Status = StatusProjeto.Rascunho;
        CriadoEm = criadoEm;
        AlteradoEm = criadoEm;
    }

    // Usado pelo driver do MongoDB na leitura.
    private Projeto()
    {
        ClienteId = string.Empty;
        Descricao = string.Empty;
    }

    public string? Id { get; private set; }

    /// <summary>Usuário dono do projeto: o <c>sub</c> do token do Keycloak (ADR-004).</summary>
    public string ClienteId { get; private set; }

    public string Descricao { get; private set; }

    public IReadOnlyList<ItemProjeto> Itens { get; private set; } = [];

    /// <summary>Nulo enquanto o projeto não foi cotado.</summary>
    public decimal? ValorTotal { get; private set; }

    public StatusProjeto Status { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset AlteradoEm { get; private set; }

    /// <summary>Controle de concorrência otimista (plano, F7).</summary>
    public int Versao { get; private set; }
}
