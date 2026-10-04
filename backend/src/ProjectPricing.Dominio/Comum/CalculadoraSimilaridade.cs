namespace ProjectPricing.Dominio.Comum;

/// <summary>
/// Similaridade da RN09: distância de Levenshtein normalizada sobre os textos normalizados
/// (minúsculas, sem acentos, sem espaços extras). Determinística; o LLM não participa.
/// </summary>
public static class CalculadoraSimilaridade
{
    /// <summary>De 0 (nada em comum) a 1 (iguais depois de normalizar).</summary>
    public static decimal Calcular(string a, string b)
    {
        var x = NormalizadorTexto.Normalizar(a);
        var y = NormalizadorTexto.Normalizar(b);
        var maior = Math.Max(x.Length, y.Length);
        if (maior == 0)
        {
            return 1m;
        }

        return 1m - ((decimal)DistanciaLevenshtein(x, y) / maior);
    }

    private static int DistanciaLevenshtein(string a, string b)
    {
        var anterior = new int[b.Length + 1];
        var atual = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            anterior[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            atual[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var custoTroca = a[i - 1] == b[j - 1] ? 0 : 1;
                atual[j] = Math.Min(Math.Min(atual[j - 1] + 1, anterior[j] + 1), anterior[j - 1] + custoTroca);
            }

            (anterior, atual) = (atual, anterior);
        }

        return anterior[b.Length];
    }
}
