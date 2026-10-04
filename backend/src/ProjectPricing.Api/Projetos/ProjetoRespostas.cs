using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Api.Projetos;

/// <summary>Projeto com a cotação (business-rules.md §8): valores com 2 casas e moeda explícita.</summary>
public sealed record ProjetoResposta(
    string Id,
    string Descricao,
    StatusProjeto Status,
    IReadOnlyList<ItemProjetoResposta> Itens,
    decimal? ValorTotal,
    string Moeda,
    DateTimeOffset CriadoEm,
    DateTimeOffset AlteradoEm)
{
    public const string MoedaPadrao = "BRL";

    public static ProjetoResposta De(Projeto projeto) => new(
        projeto.Id ?? throw new InvalidOperationException("Projeto sem id não pode ser devolvido."),
        projeto.Descricao,
        projeto.Status,
        [.. projeto.Itens.Select(ItemProjetoResposta.De)],
        projeto.ValorTotal,
        MoedaPadrao,
        projeto.CriadoEm,
        projeto.AlteradoEm);
}

/// <summary>Item com o nome e o preço congelados na cotação (RN02).</summary>
public sealed record ItemProjetoResposta(
    string MaterialId,
    string Nome,
    decimal Quantidade,
    UnidadeMedida Unidade,
    decimal PrecoUnitario,
    decimal Subtotal)
{
    public static ItemProjetoResposta De(ItemProjeto item) => new(
        item.MaterialId, item.NomeSnapshot, item.Quantidade, item.Unidade, item.PrecoUnitarioSnapshot, item.Subtotal);
}

/// <summary>Linha da listagem de projetos, sem os itens.</summary>
public sealed record ProjetoResumoResposta(
    string Id,
    string Descricao,
    StatusProjeto Status,
    decimal? ValorTotal,
    string Moeda,
    DateTimeOffset CriadoEm,
    DateTimeOffset AlteradoEm)
{
    public static ProjetoResumoResposta De(Projeto projeto) => new(
        projeto.Id!, projeto.Descricao, projeto.Status, projeto.ValorTotal, ProjetoResposta.MoedaPadrao, projeto.CriadoEm, projeto.AlteradoEm);
}

public sealed record MensagemResposta(PapelMensagem Papel, string Conteudo, DateTimeOffset EnviadaEm)
{
    public static MensagemResposta De(Mensagem mensagem) => new(mensagem.Papel, mensagem.Conteudo, mensagem.EnviadaEm);
}

/// <summary>Dados do evento fim: o estado do projeto ao final da rodada.</summary>
public sealed record FimResposta(string ProjetoId, StatusProjeto Status);

/// <summary>Dados do evento delta: texto da resposta do agente.</summary>
public sealed record DeltaResposta(string Texto);
