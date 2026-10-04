using System.Text;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using ProjectPricing.Dominio.Comum;

namespace ProjectPricing.Infraestrutura.Mongo;

/// <summary>
/// Expressão regular que ignora maiúsculas e acentos ("pelicula" encontra "Película").
/// A collation do MongoDB não vale para $regex; por isso cada letra acentuável vira uma classe de caracteres.
/// </summary>
public static class RegexSemAcento
{
    private static readonly Dictionary<char, string> Variantes = new()
    {
        ['a'] = "[aáàâãä]",
        ['e'] = "[eéèêë]",
        ['i'] = "[iíìîï]",
        ['o'] = "[oóòôõö]",
        ['u'] = "[uúùûü]",
        ['c'] = "[cç]",
        ['n'] = "[nñ]",
    };

    /// <summary>Trecho em qualquer posição do texto.</summary>
    public static BsonRegularExpression Contendo(string termo) => new(Padrao(termo), "i");

    /// <summary>Texto inteiro igual ao termo.</summary>
    public static BsonRegularExpression Exato(string termo) => new($"^{Padrao(termo)}$", "i");

    private static string Padrao(string termo)
    {
        var normalizado = NormalizadorTexto.Normalizar(termo);
        var padrao = new StringBuilder(normalizado.Length * 4);

        foreach (var caractere in normalizado)
        {
            padrao.Append(Variantes.TryGetValue(caractere, out var variantes)
                ? variantes
                : Regex.Escape(caractere.ToString()));
        }

        return padrao.ToString();
    }
}
