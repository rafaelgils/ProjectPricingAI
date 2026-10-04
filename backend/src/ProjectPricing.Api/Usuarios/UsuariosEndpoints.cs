using Microsoft.AspNetCore.Http.HttpResults;
using ProjectPricing.Aplicacao.Usuarios;

namespace ProjectPricing.Api.Usuarios;

public sealed record PerfilUsuarioResposta(
    string KeycloakId,
    string Nome,
    string? Email,
    IReadOnlyCollection<string> Papeis);

public static class UsuariosEndpoints
{
    public static RouteGroupBuilder MapearUsuarios(this RouteGroupBuilder api)
    {
        var usuarios = api.MapGroup("/usuarios").WithTags("Usuários");

        // Todos os perfis autenticados (catálogo da API, architecture.md §4).
        usuarios.MapGet("/me", ObterPerfil)
            .WithName("ObterPerfilDoUsuarioLogado")
            .WithSummary("Perfil e papéis do usuário logado");

        return api;
    }

    public static Ok<PerfilUsuarioResposta> ObterPerfil(IUsuarioAtual usuario)
    {
        return TypedResults.Ok(new PerfilUsuarioResposta(
            usuario.KeycloakId,
            usuario.Nome,
            usuario.Email,
            usuario.Papeis));
    }
}
