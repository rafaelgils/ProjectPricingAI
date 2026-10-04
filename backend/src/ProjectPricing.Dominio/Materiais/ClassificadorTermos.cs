using System.Text.Json.Serialization;
using ProjectPricing.Dominio.Comum;

namespace ProjectPricing.Dominio.Materiais;

/// <summary>Resultado do passo 1 da RN09 para um termo da descrição.</summary>
public enum ClassificacaoTermo
{
    /// <summary>Similaridade de 100% com o nome ou um sinônimo.</summary>
    [JsonStringEnumMemberName("encontrado")]
    Encontrado,

    /// <summary>De 80% a menos de 100%: o cliente confirma.</summary>
    [JsonStringEnumMemberName("aConfirmar")]
    AConfirmar,

    /// <summary>Abaixo de 80%: segue para o passo 2 (sugestão do agente).</summary>
    [JsonStringEnumMemberName("semCorrespondencia")]
    SemCorrespondencia,
}

/// <param name="Material">Material de maior similaridade; nulo se o catálogo estiver vazio.</param>
public sealed record ResultadoTermo(string Termo, Material? Material, decimal Similaridade, ClassificacaoTermo Classificacao);

/// <summary>Passo 1 da RN09: compara cada termo com o nome e os sinônimos dos materiais ativos.</summary>
public static class ClassificadorTermos
{
    public const decimal LimiarSimilaridade = 0.80m;
    private const decimal SimilaridadeTotal = 1m;

    public static ResultadoTermo Classificar(string termo, IReadOnlyCollection<Material> materiaisAtivos)
    {
        ArgumentNullException.ThrowIfNull(termo);
        ArgumentNullException.ThrowIfNull(materiaisAtivos);

        Material? melhor = null;
        var maiorSimilaridade = 0m;
        foreach (var material in materiaisAtivos)
        {
            var similaridade = material.NomeESinonimos.Max(nome => CalculadoraSimilaridade.Calcular(termo, nome));
            if (melhor is null || similaridade > maiorSimilaridade)
            {
                melhor = material;
                maiorSimilaridade = similaridade;
            }
        }

        var classificacao = maiorSimilaridade switch
        {
            SimilaridadeTotal => ClassificacaoTermo.Encontrado,
            >= LimiarSimilaridade => ClassificacaoTermo.AConfirmar,
            _ => ClassificacaoTermo.SemCorrespondencia,
        };

        return new ResultadoTermo(termo, melhor, Math.Round(maiorSimilaridade, 2), classificacao);
    }
}
