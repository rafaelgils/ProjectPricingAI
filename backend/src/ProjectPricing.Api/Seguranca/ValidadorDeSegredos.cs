namespace ProjectPricing.Api.Seguranca;

/// <summary>
/// Falha a subida quando um segredo ainda tem o valor de exemplo do <c>.env.example</c> (ADR-013). Um segredo
/// esquecido com "troque-esta-senha" deixaria o ambiente protegido por uma senha pública.
/// </summary>
public static class ValidadorDeSegredos
{
    public const string PrefixoDeExemplo = "troque-";

    /// <summary>Chaves de configuração que guardam segredos, para varredura de valores de exemplo.</summary>
    public static readonly IReadOnlyList<string> ChavesSecretas =
    [
        "ConnectionStrings:MongoDB",
        "Keycloak:AdminClientSecret",
        "Anthropic:ApiKey",
    ];

    /// <exception cref="InvalidOperationException">Algum segredo mantém o valor de exemplo.</exception>
    public static void Validar(IConfiguration configuracao)
    {
        var pendentes = ChavesSecretas
            .Where(chave => configuracao[chave] is { } valor
                && valor.Contains(PrefixoDeExemplo, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pendentes.Count > 0)
        {
            throw new InvalidOperationException(
                "Segredos com valor de exemplo (\"" + PrefixoDeExemplo + "...\"): " + string.Join(", ", pendentes) +
                ". Troque-os no .env antes de subir o ambiente.");
        }
    }
}
