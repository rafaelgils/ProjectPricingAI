namespace ProjectPricing.Aplicacao.Usuarios;

/// <summary>Usuário autenticado da requisição, lido do JWT emitido pelo Keycloak.</summary>
public interface IUsuarioAtual
{
    /// <summary>Claim <c>sub</c>; é o <c>clienteId</c> dos projetos.</summary>
    string KeycloakId { get; }

    string Nome { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Papeis { get; }

    bool EhAdmin { get; }
}
