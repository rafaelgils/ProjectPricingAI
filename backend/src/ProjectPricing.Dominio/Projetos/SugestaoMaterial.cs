using System.Text.Json.Serialization;

namespace ProjectPricing.Dominio.Projetos;

/// <summary>Material proposto para um termo da descrição; sempre exige confirmação do cliente (RN09).</summary>
/// <param name="Similaridade">Só quando <paramref name="Origem"/> é similaridade; nulo na sugestão do agente.</param>
public sealed record SugestaoMaterial(
    string Termo,
    string MaterialId,
    string Nome,
    OrigemSugestao Origem,
    decimal? Similaridade);

public enum OrigemSugestao
{
    [JsonStringEnumMemberName("similaridade")]
    Similaridade,

    [JsonStringEnumMemberName("agente")]
    Agente,
}
