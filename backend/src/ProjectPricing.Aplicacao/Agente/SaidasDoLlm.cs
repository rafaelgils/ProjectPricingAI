using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ProjectPricing.Aplicacao.Agente;

/// <summary>Saída estruturada da interpretação (ADR-001): itens e medidas, nunca valores.</summary>
public sealed record InterpretacaoLlm(
    [property: Description("itens quando todas as medidas estão claras; esclarecimento quando falta informação ou o pedido não é permitido.")]
    TipoInterpretacao Tipo,
    [property: Description("Pergunta ao cliente, só quando o tipo é esclarecimento.")]
    string? Pergunta,
    [property: Description("Materiais e serviços do projeto com a medida de cada um.")]
    IReadOnlyList<ItemInterpretado> Itens);

public enum TipoInterpretacao
{
    [JsonStringEnumMemberName("itens")]
    Itens,

    [JsonStringEnumMemberName("esclarecimento")]
    Esclarecimento,
}

public sealed record ItemInterpretado(
    [property: Description("Palavras do cliente para o item.")] string Termo,
    [property: Description("Id do material: o encontrado por buscarMateriais, o de um item atual do projeto ou o de uma sugestão confirmada; nulo nos outros casos.")] string? MaterialId,
    [property: Description("Verdadeiro só quando o cliente confirmou a sugestão deste material na conversa.")] bool Confirmado,
    [property: Description("Quantidade na unidade informada; com largura e altura, é o número de peças.")] decimal? Quantidade,
    [property: Description("Unidade informada: mm, cm, m, mm2, cm2, m2, ml, L, min, h ou un.")] string Unidade,
    [property: Description("Largura, para itens vendidos por área.")] decimal? Largura,
    [property: Description("Altura, para itens vendidos por área.")] decimal? Altura);

/// <summary>Saída estruturada do passo 2 da RN09.</summary>
public sealed record SugestoesLlm(IReadOnlyList<SugestaoLlm> Sugestoes);

public sealed record SugestaoLlm(
    [property: Description("O termo do cliente, exatamente como recebido.")] string Termo,
    [property: Description("Id de um item da lista do catálogo com o mesmo sentido, ou nulo.")] string? MaterialId);
