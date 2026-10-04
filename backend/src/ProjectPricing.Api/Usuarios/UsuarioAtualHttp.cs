using System.Security.Claims;
using ProjectPricing.Aplicacao.Usuarios;

namespace ProjectPricing.Api.Usuarios;

/// <summary>Lê o usuário das claims do JWT validado na requisição atual.</summary>
public sealed class UsuarioAtualHttp(IHttpContextAccessor acessorHttp) : IUsuarioAtual
{
    private ClaimsPrincipal Principal => acessorHttp.HttpContext?.User
        ?? throw new InvalidOperationException("Não há requisição HTTP em andamento.");

    public string KeycloakId => Principal.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Token sem a claim \"sub\".");

    public string Nome => Principal.FindFirstValue("name")
        ?? Principal.FindFirstValue("preferred_username")
        ?? KeycloakId;

    public string? Email => Principal.FindFirstValue("email");

    public IReadOnlyCollection<string> Papeis => [.. Principal.Identities
        .SelectMany(i => i.FindAll(i.RoleClaimType))
        .Select(c => c.Value)
        .Distinct(StringComparer.Ordinal)];

    public bool EhAdmin => Principal.IsInRole(Aplicacao.Usuarios.Papeis.Admin);
}
