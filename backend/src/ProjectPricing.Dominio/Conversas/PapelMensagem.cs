using System.Text.Json.Serialization;

namespace ProjectPricing.Dominio.Conversas;

public enum PapelMensagem
{
    [JsonStringEnumMemberName("usuario")]
    Usuario,

    [JsonStringEnumMemberName("assistente")]
    Assistente,

    [JsonStringEnumMemberName("tool")]
    Tool,
}
