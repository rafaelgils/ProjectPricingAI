using ProjectPricing.Dominio.Excecoes;

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

    /// <summary>Material já cotado neste projeto: mantém o preço congelado no refinamento (RN02).</summary>
    public ItemProjeto? ItemDoMaterial(string materialId) =>
        Itens.FirstOrDefault(i => i.MaterialId == materialId);

    /// <summary>
    /// Grava a cotação calculada pelo Serviço de Precificação (RN01). Na primeira vez o projeto passa
    /// de rascunho para cotado; no refinamento, a lista de itens é substituída (RN02).
    /// </summary>
    public void RegistrarCotacao(IReadOnlyList<ItemProjeto> itens, decimal valorTotal, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(itens);
        GarantirQueNaoEstaArquivado();
        if (itens.Count == 0)
        {
            throw new ArgumentException("A cotação precisa de ao menos um item.", nameof(itens));
        }

        Itens = [.. itens];
        ValorTotal = valorTotal;
        Status = StatusProjeto.Cotado;
        AlteradoEm = agora;
    }

    /// <summary>Exclusão lógica (RN04): o projeto vira somente leitura (RN11). Arquivar de novo não muda nada.</summary>
    public void Arquivar(DateTimeOffset agora)
    {
        if (Status == StatusProjeto.Arquivado)
        {
            return;
        }

        Status = StatusProjeto.Arquivado;
        AlteradoEm = agora;
    }

    /// <summary>
    /// Concorrência otimista: o repositório grava só se a versão no banco for a lida, e então a avança.
    /// </summary>
    public void AvancarVersao() => Versao++;

    /// <summary>Projeto arquivado é somente leitura (RN11).</summary>
    public void GarantirQueNaoEstaArquivado()
    {
        if (Status == StatusProjeto.Arquivado)
        {
            throw new ProjetoArquivadoException();
        }
    }
}
