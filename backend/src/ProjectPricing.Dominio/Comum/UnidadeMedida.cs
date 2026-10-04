using System.Text.Json.Serialization;

namespace ProjectPricing.Dominio.Comum;

/// <summary>Unidades do catálogo (business-rules.md §7). O nome serializado é o código da documentação.</summary>
public enum UnidadeMedida
{
    [JsonStringEnumMemberName("m")]
    Metro,

    [JsonStringEnumMemberName("m2")]
    MetroQuadrado,

    [JsonStringEnumMemberName("un")]
    Unidade,

    [JsonStringEnumMemberName("L")]
    Litro,

    [JsonStringEnumMemberName("h")]
    Hora,
}
