using System.Text.Json.Serialization;

namespace ProjectPricing.Dominio.Materiais;

public enum TipoMaterial
{
    [JsonStringEnumMemberName("material")]
    Material,

    [JsonStringEnumMemberName("servico")]
    Servico,
}
