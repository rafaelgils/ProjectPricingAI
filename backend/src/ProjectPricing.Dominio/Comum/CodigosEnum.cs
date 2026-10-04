using System.Reflection;
using System.Text.Json.Serialization;

namespace ProjectPricing.Dominio.Comum;

/// <summary>
/// Código de cada valor de enum do domínio (ex.: <c>UnidadeMedida.MetroQuadrado</c> → "m2"), lido do
/// <see cref="JsonStringEnumMemberNameAttribute"/>. É o mesmo código na API, no banco e na documentação.
/// </summary>
public static class CodigosEnum<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> CodigoPorValor = Enum.GetValues<TEnum>()
        .ToDictionary(valor => valor, LerCodigo);

    private static readonly Dictionary<string, TEnum> ValorPorCodigo = CodigoPorValor
        .ToDictionary(par => par.Value, par => par.Key, StringComparer.Ordinal);

    public static IReadOnlyCollection<string> Codigos => ValorPorCodigo.Keys;

    public static string Codigo(TEnum valor) => CodigoPorValor[valor];

    /// <summary>Compara o código exato, diferenciando maiúsculas ("L" é litro).</summary>
    public static bool TentarConverter(string? codigo, out TEnum valor)
    {
        if (codigo is not null && ValorPorCodigo.TryGetValue(codigo, out valor))
        {
            return true;
        }

        valor = default;
        return false;
    }

    private static string LerCodigo(TEnum valor)
    {
        var nome = valor.ToString();
        var atributo = typeof(TEnum).GetField(nome)?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();

        return atributo?.Name ?? nome;
    }
}
