using System.Globalization;
using System.Text;

namespace ProjectPricing.Dominio.Comum;

/// <summary>
/// Normalização usada nas comparações de nomes (RN09, RN10): minúsculas, sem acentos e sem espaços extras.
/// </summary>
public static class NormalizadorTexto
{
    public static string Normalizar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(decomposto.Length);
        var espacoPendente = false;

        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(caractere))
            {
                espacoPendente = resultado.Length > 0;
                continue;
            }

            if (espacoPendente)
            {
                resultado.Append(' ');
                espacoPendente = false;
            }

            resultado.Append(char.ToLowerInvariant(caractere));
        }

        return resultado.ToString();
    }

    public static bool SaoEquivalentes(string a, string b) =>
        string.Equals(Normalizar(a), Normalizar(b), StringComparison.Ordinal);
}
