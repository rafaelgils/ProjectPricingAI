using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Aplicacao.Usuarios;

/// <summary>Usuário como o Keycloak o guarda (ADR-004: o Keycloak é a única fonte de usuários).</summary>
/// <param name="Papel">Um dos papéis do sistema; nulo se o usuário não tiver nenhum deles.</param>
public sealed record UsuarioGerenciado(
    string Id,
    string Usuario,
    string Nome,
    string Sobrenome,
    string? Email,
    string? Papel,
    bool Ativo,
    DateTimeOffset CriadoEm);

public sealed record DadosNovoUsuario(
    string Usuario,
    string Nome,
    string Sobrenome,
    string Email,
    string Papel,
    string SenhaTemporaria);

public sealed record DadosAlteracaoUsuario(string Nome, string Sobrenome, string Email, string Papel, bool Ativo);

public sealed record PaginaDeUsuarios(IReadOnlyList<UsuarioGerenciado> Itens, long Total);

/// <summary>Porta para a Keycloak Admin API (RF10). A API /usuarios só repassa para ela.</summary>
public interface IGestaoIdentidade
{
    Task<PaginaDeUsuarios> ListarAsync(string? busca, int pagina, int tamanho, CancellationToken cancellationToken);

    Task<UsuarioGerenciado?> ObterAsync(string id, CancellationToken cancellationToken);

    /// <summary>Cria o usuário ativo, com a senha temporária (troca obrigatória no primeiro login).</summary>
    /// <returns>O id do usuário criado.</returns>
    /// <exception cref="UsuarioDuplicadoException">Usuário ou e-mail já existe.</exception>
    Task<string> CriarAsync(DadosNovoUsuario dados, CancellationToken cancellationToken);

    /// <exception cref="UsuarioDuplicadoException">O e-mail já é de outro usuário.</exception>
    Task AtualizarAsync(string id, string nome, string sobrenome, string email, bool ativo, CancellationToken cancellationToken);

    /// <summary>Deixa o usuário com exatamente um dos papéis do sistema.</summary>
    Task DefinirPapelAsync(string id, string papel, CancellationToken cancellationToken);
}

/// <summary>Casos de uso da gestão de usuários pelo Admin (RF10). Não há autocadastro.</summary>
public sealed class ServicoUsuarios(IGestaoIdentidade identidade, IUsuarioAtual usuarioAtual)
{
    public static readonly IReadOnlyList<string> PapeisDoSistema = [Papeis.Admin, Papeis.ClienteInterno, Papeis.ClienteExterno];

    private const string Recurso = "Usuário";

    public Task<PaginaDeUsuarios> ListarAsync(string? busca, int pagina, int tamanho, CancellationToken cancellationToken) =>
        identidade.ListarAsync(busca, pagina, tamanho, cancellationToken);

    public async Task<UsuarioGerenciado> ObterAsync(string id, CancellationToken cancellationToken) =>
        await identidade.ObterAsync(id, cancellationToken) ?? throw new RecursoNaoEncontradoException(Recurso, id);

    public async Task<UsuarioGerenciado> CriarAsync(DadosNovoUsuario dados, CancellationToken cancellationToken)
    {
        var id = await identidade.CriarAsync(dados, cancellationToken);
        await identidade.DefinirPapelAsync(id, dados.Papel, cancellationToken);
        return await ObterAsync(id, cancellationToken);
    }

    public async Task<UsuarioGerenciado> AlterarAsync(string id, DadosAlteracaoUsuario dados, CancellationToken cancellationToken)
    {
        await ObterAsync(id, cancellationToken);
        if (id == usuarioAtual.KeycloakId && (!dados.Ativo || dados.Papel != Papeis.Admin))
        {
            throw new AlteracaoDoProprioUsuarioException();
        }

        await identidade.AtualizarAsync(id, dados.Nome, dados.Sobrenome, dados.Email, dados.Ativo, cancellationToken);
        await identidade.DefinirPapelAsync(id, dados.Papel, cancellationToken);
        return await ObterAsync(id, cancellationToken);
    }

    /// <summary>Exclusão lógica (standards.md §5): o usuário é desativado, não apagado.</summary>
    public async Task DesativarAsync(string id, CancellationToken cancellationToken)
    {
        if (id == usuarioAtual.KeycloakId)
        {
            throw new AlteracaoDoProprioUsuarioException();
        }

        var usuario = await ObterAsync(id, cancellationToken);
        await identidade.AtualizarAsync(id, usuario.Nome, usuario.Sobrenome, usuario.Email ?? string.Empty, ativo: false, cancellationToken);
    }
}
