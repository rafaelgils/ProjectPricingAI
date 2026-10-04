using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Infraestrutura.Keycloak;

/// <summary>Seção "Keycloak" da configuração, na parte usada pela Admin API.</summary>
public sealed class OpcoesKeycloakAdmin
{
    /// <summary>URL do realm na rede interna (ex.: http://keycloak:8080/realms/precificacao).</summary>
    public string UrlRealm { get; init; } = string.Empty;

    public string ClientId { get; init; } = "precificacao-admin";

    /// <summary>Segredo do cliente confidencial; vem do .env (ou do cofre fora do ambiente local).</summary>
    public string? ClientSecret { get; init; }

    /// <summary>http://host/realms/X → http://host/admin/realms/X</summary>
    public Uri UrlAdmin
    {
        get
        {
            var realm = new Uri(UrlRealm.TrimEnd('/'));
            return new Uri($"{realm.GetLeftPart(UriPartial.Authority)}/admin{realm.AbsolutePath}/");
        }
    }

    public Uri UrlToken => new($"{UrlRealm.TrimEnd('/')}/protocol/openid-connect/token");
}

/// <summary>
/// Repasse à Keycloak Admin API (ADR-004, RF10) com a service account do cliente precificacao-admin,
/// que só tem os papéis view-users e manage-users.
/// </summary>
public sealed class KeycloakAdminClient(HttpClient http, OpcoesKeycloakAdmin opcoes, TimeProvider relogio) : IGestaoIdentidade
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Renova o token um pouco antes de expirar, para não falhar no meio de uma chamada.</summary>
    private static readonly TimeSpan MargemExpiracao = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _travaToken = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiraEm;

    public async Task<PaginaDeUsuarios> ListarAsync(string? busca, int pagina, int tamanho, CancellationToken cancellationToken)
    {
        var filtro = string.IsNullOrWhiteSpace(busca) ? string.Empty : $"search={Uri.EscapeDataString(busca.Trim())}&";
        var usuarios = await LerAsync<List<UsuarioKeycloak>>(
            $"users?{filtro}first={(pagina - 1) * tamanho}&max={tamanho}&briefRepresentation=true", cancellationToken) ?? [];
        var total = await LerAsync<long>($"users/count?{filtro}".TrimEnd('&', '?'), cancellationToken);

        var itens = new List<UsuarioGerenciado>(usuarios.Count);
        foreach (var usuario in usuarios)
        {
            itens.Add(ParaUsuario(usuario, await PapelDoSistemaAsync(usuario.Id!, cancellationToken)));
        }

        return new PaginaDeUsuarios(itens, total);
    }

    public async Task<UsuarioGerenciado?> ObterAsync(string id, CancellationToken cancellationToken)
    {
        var usuario = await LerAsync<UsuarioKeycloak>($"users/{Uri.EscapeDataString(id)}", cancellationToken, nuloSeNaoExistir: true);
        return usuario is null ? null : ParaUsuario(usuario, await PapelDoSistemaAsync(usuario.Id!, cancellationToken));
    }

    public async Task<string> CriarAsync(DadosNovoUsuario dados, CancellationToken cancellationToken)
    {
        var usuario = new UsuarioKeycloak
        {
            Username = dados.Usuario,
            FirstName = dados.Nome,
            LastName = dados.Sobrenome,
            Email = dados.Email,
            EmailVerified = true,
            Enabled = true,
            // Senha temporária: o Keycloak exige a troca no primeiro login.
            Credentials = [new CredencialKeycloak { Type = "password", Value = dados.SenhaTemporaria, Temporary = true }],
        };

        using var resposta = await EnviarAsync(HttpMethod.Post, "users", usuario, cancellationToken);
        GarantirSucesso(resposta);
        var local = resposta.Headers.Location
            ?? throw new InvalidOperationException("O Keycloak não devolveu o endereço do usuário criado.");
        return local.Segments[^1];
    }

    public async Task AtualizarAsync(string id, string nome, string sobrenome, string email, bool ativo, CancellationToken cancellationToken)
    {
        var alteracao = new UsuarioKeycloak { FirstName = nome, LastName = sobrenome, Email = email, Enabled = ativo };
        using var resposta = await EnviarAsync(HttpMethod.Put, $"users/{Uri.EscapeDataString(id)}", alteracao, cancellationToken);
        GarantirSucesso(resposta);
    }

    public async Task DefinirPapelAsync(string id, string papel, CancellationToken cancellationToken)
    {
        var caminho = $"users/{Uri.EscapeDataString(id)}/role-mappings/realm";
        var atuais = await LerAsync<List<PapelKeycloak>>(caminho, cancellationToken) ?? [];

        var remover = atuais.Where(p => ServicoUsuarios.PapeisDoSistema.Contains(p.Name) && p.Name != papel).ToList();
        if (remover.Count > 0)
        {
            using var resposta = await EnviarAsync(HttpMethod.Delete, caminho, remover, cancellationToken);
            GarantirSucesso(resposta);
        }

        if (atuais.Any(p => p.Name == papel))
        {
            return;
        }

        // O id do papel vem da lista de papéis disponíveis para o usuário: a service account não lê o realm.
        var disponiveis = await LerAsync<List<PapelKeycloak>>($"{caminho}/available", cancellationToken) ?? [];
        var novo = disponiveis.FirstOrDefault(p => p.Name == papel)
            ?? throw new InvalidOperationException($"Papel \"{papel}\" não existe no realm.");
        using var adicao = await EnviarAsync(HttpMethod.Post, caminho, new[] { novo }, cancellationToken);
        GarantirSucesso(adicao);
    }

    private async Task<string?> PapelDoSistemaAsync(string id, CancellationToken cancellationToken)
    {
        var papeis = await LerAsync<List<PapelKeycloak>>($"users/{Uri.EscapeDataString(id)}/role-mappings/realm", cancellationToken) ?? [];
        return ServicoUsuarios.PapeisDoSistema.FirstOrDefault(sistema => papeis.Any(p => p.Name == sistema));
    }

    private static UsuarioGerenciado ParaUsuario(UsuarioKeycloak usuario, string? papel) => new(
        usuario.Id!,
        usuario.Username ?? string.Empty,
        usuario.FirstName ?? string.Empty,
        usuario.LastName ?? string.Empty,
        usuario.Email,
        papel,
        usuario.Enabled ?? false,
        DateTimeOffset.FromUnixTimeMilliseconds(usuario.CreatedTimestamp ?? 0));

    private async Task<T?> LerAsync<T>(string caminho, CancellationToken cancellationToken, bool nuloSeNaoExistir = false)
    {
        using var resposta = await EnviarAsync(HttpMethod.Get, caminho, corpo: null, cancellationToken);
        if (nuloSeNaoExistir && resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        GarantirSucesso(resposta);
        return await resposta.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string caminho, object? corpo, CancellationToken cancellationToken)
    {
        using var requisicao = new HttpRequestMessage(metodo, new Uri(opcoes.UrlAdmin, caminho));
        requisicao.Headers.Authorization = new("Bearer", await ObterTokenAsync(cancellationToken));
        if (corpo is not null)
        {
            requisicao.Content = JsonContent.Create(corpo, corpo.GetType(), options: Json);
        }

        return await http.SendAsync(requisicao, cancellationToken);
    }

    private static void GarantirSucesso(HttpResponseMessage resposta)
    {
        if (resposta.StatusCode == HttpStatusCode.Conflict)
        {
            throw new UsuarioDuplicadoException();
        }

        resposta.EnsureSuccessStatusCode();
    }

    /// <summary>Token da service account (client_credentials), guardado até perto de expirar.</summary>
    private async Task<string> ObterTokenAsync(CancellationToken cancellationToken)
    {
        await _travaToken.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && relogio.GetUtcNow() < _expiraEm)
            {
                return _token;
            }

            using var resposta = await http.PostAsync(opcoes.UrlToken, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = opcoes.ClientId,
                ["client_secret"] = opcoes.ClientSecret ?? string.Empty,
            }), cancellationToken);
            resposta.EnsureSuccessStatusCode();

            var token = await resposta.Content.ReadFromJsonAsync<TokenKeycloak>(Json, cancellationToken)
                ?? throw new InvalidOperationException("Resposta de token vazia do Keycloak.");
            _token = token.AccessToken;
            _expiraEm = relogio.GetUtcNow().AddSeconds(token.ExpiresIn) - MargemExpiracao;
            return _token;
        }
        finally
        {
            _travaToken.Release();
        }
    }

    private sealed class UsuarioKeycloak
    {
        public string? Id { get; init; }
        public string? Username { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Email { get; init; }
        public bool? EmailVerified { get; init; }
        public bool? Enabled { get; init; }
        public long? CreatedTimestamp { get; init; }
        public IReadOnlyList<CredencialKeycloak>? Credentials { get; init; }
    }

    private sealed class CredencialKeycloak
    {
        public string Type { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public bool Temporary { get; init; }
    }

    private sealed class PapelKeycloak
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
    }

    private sealed class TokenKeycloak
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
