using System.Security.Claims;
using System.Text.Json;

namespace ProjectPricing.Api.Autenticacao;

/// <summary>
/// O Keycloak envia os realm roles dentro da claim <c>realm_access</c> (<c>{"roles":["admin"]}</c>).
/// Aqui eles viram claims de papel da identidade, para as policies usarem <c>RequireRole</c>.
/// </summary>
public static class PapeisKeycloak
{
    public const string ClaimRealmAccess = "realm_access";

    public static void AdicionarPapeis(ClaimsPrincipal principal)
    {
        foreach (var identidade in principal.Identities.Where(i => i.IsAuthenticated))
        {
            var realmAccess = identidade.FindFirst(ClaimRealmAccess)?.Value;
            foreach (var papel in LerPapeis(realmAccess))
            {
                if (!identidade.HasClaim(identidade.RoleClaimType, papel))
                {
                    identidade.AddClaim(new Claim(identidade.RoleClaimType, papel));
                }
            }
        }
    }

    public static IReadOnlyList<string> LerPapeis(string? realmAccessJson)
    {
        if (string.IsNullOrWhiteSpace(realmAccessJson))
        {
            return [];
        }

        try
        {
            using var documento = JsonDocument.Parse(realmAccessJson);
            if (!documento.RootElement.TryGetProperty("roles", out var papeis) || papeis.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return [.. papeis.EnumerateArray()
                .Where(p => p.ValueKind == JsonValueKind.String)
                .Select(p => p.GetString()!)];
        }
        catch (JsonException)
        {
            // Claim malformada não concede papel nenhum.
            return [];
        }
    }
}
