using System.Text.Json.Serialization;

namespace ProjectPricing.Dominio.Projetos;

/// <summary>Ciclo de vida do projeto (business-rules.md §6).</summary>
public enum StatusProjeto
{
    [JsonStringEnumMemberName("rascunho")]
    Rascunho,

    [JsonStringEnumMemberName("cotado")]
    Cotado,

    [JsonStringEnumMemberName("arquivado")]
    Arquivado,
}
