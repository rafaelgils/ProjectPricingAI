namespace ProjectPricing.Aplicacao.Usuarios;

/// <summary>
/// Realm roles do Keycloak (ADR-004). Único lugar com os nomes dos papéis (standards.md §3).
/// </summary>
public static class Papeis
{
    public const string Admin = "admin";
    public const string ClienteInterno = "cliente-interno";
    public const string ClienteExterno = "cliente-externo";
}
