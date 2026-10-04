using System.Text.Json.Serialization;

namespace ProjectPricing.Dominio.Materiais;

public enum StatusMaterial
{
    [JsonStringEnumMemberName("ativo")]
    Ativo,

    [JsonStringEnumMemberName("inativo")]
    Inativo,
}
