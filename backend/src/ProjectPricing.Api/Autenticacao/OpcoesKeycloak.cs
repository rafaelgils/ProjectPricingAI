namespace ProjectPricing.Api.Autenticacao;

/// <summary>Seção "Keycloak" da configuração.</summary>
public sealed class OpcoesKeycloak
{
    public const string Secao = "Keycloak";

    /// <summary>URL do realm como o navegador vê; é o "iss" dos tokens.</summary>
    public string UrlPublica { get; init; } = string.Empty;

    /// <summary>URL do realm na rede interna, usada para ler os metadados e as chaves (plano, F2).</summary>
    public string UrlInterna { get; init; } = string.Empty;

    public string Audiencia { get; init; } = string.Empty;

    /// <summary>Falso só no ambiente local, que usa HTTP (RNF01).</summary>
    public bool ExigirHttpsMetadata { get; init; } = true;
}
