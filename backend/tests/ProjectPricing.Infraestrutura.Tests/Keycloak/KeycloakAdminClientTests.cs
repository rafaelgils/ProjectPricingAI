using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Moq;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Infraestrutura.Keycloak;

namespace ProjectPricing.Infraestrutura.Tests.Keycloak;

public class KeycloakAdminClientTests
{
    private const string Admin = "http://keycloak:8080/admin/realms/precificacao/";
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);
    private static readonly OpcoesKeycloakAdmin Opcoes = new()
    {
        UrlRealm = "http://keycloak:8080/realms/precificacao/",
        ClientSecret = "segredo",
    };

    private readonly KeycloakFalso _keycloak = new();
    private readonly Mock<TimeProvider> _relogio = new();

    public KeycloakAdminClientTests()
    {
        _relogio.Setup(r => r.GetUtcNow()).Returns(Agora);
    }

    private KeycloakAdminClient Cliente => new(new HttpClient(_keycloak), Opcoes, _relogio.Object);

    [Fact]
    public void Urls_sao_derivadas_do_realm()
    {
        Assert.Equal(Admin, Opcoes.UrlAdmin.ToString());
        Assert.Equal("http://keycloak:8080/realms/precificacao/protocol/openid-connect/token", Opcoes.UrlToken.ToString());
    }

    [Fact]
    public async Task Listar_pagina_conta_e_traz_o_papel_do_sistema()
    {
        _keycloak.Responder("GET", "users?search=mar&first=20&max=10&briefRepresentation=true",
            """[{"id":"u1","username":"maria","firstName":"Maria","lastName":"Silva","email":"m@e.com","enabled":true,"createdTimestamp":1791135000000}]""");
        _keycloak.Responder("GET", "users/count?search=mar", "41");
        _keycloak.Responder("GET", "users/u1/role-mappings/realm",
            """[{"id":"r0","name":"default-roles-precificacao"},{"id":"r2","name":"cliente-interno"}]""");

        var pagina = await Cliente.ListarAsync(" mar ", 3, 10, CancellationToken.None);

        Assert.Equal(41, pagina.Total);
        var usuario = Assert.Single(pagina.Itens);
        Assert.Equal(new UsuarioGerenciado("u1", "maria", "Maria", "Silva", "m@e.com", "cliente-interno", true,
            DateTimeOffset.FromUnixTimeMilliseconds(1791135000000)), usuario);
        Assert.All(_keycloak.Requisicoes.Where(r => r.Caminho != "token"), r => Assert.Equal("Bearer tk-1", r.Autorizacao));
    }

    [Fact]
    public async Task Token_da_service_account_e_reaproveitado_ate_perto_de_expirar()
    {
        _keycloak.Responder("GET", "users/count", "0");
        var cliente = Cliente;

        await cliente.ListarAsync(null, 1, 20, CancellationToken.None);
        await cliente.ListarAsync(null, 1, 20, CancellationToken.None);
        Assert.Equal(1, _keycloak.Requisicoes.Count(r => r.Caminho == "token"));
        var pedido = _keycloak.Requisicoes.First(r => r.Caminho == "token").Corpo;
        Assert.Contains("grant_type=client_credentials", pedido);
        Assert.Contains("client_id=precificacao-admin", pedido);
        Assert.Contains("client_secret=segredo", pedido);

        // expires_in = 300 s e margem de 30 s: aos 271 s já renova.
        _relogio.Setup(r => r.GetUtcNow()).Returns(Agora.AddSeconds(271));
        await cliente.ListarAsync(null, 1, 20, CancellationToken.None);
        Assert.Equal(2, _keycloak.Requisicoes.Count(r => r.Caminho == "token"));
    }

    [Fact]
    public async Task Obter_inexistente_devolve_nulo()
    {
        Assert.Null(await Cliente.ObterAsync("nao-existe", CancellationToken.None));
    }

    [Fact]
    public async Task Criar_envia_senha_temporaria_e_devolve_o_id_do_Location()
    {
        _keycloak.Responder("POST", "users", string.Empty, HttpStatusCode.Created, location: $"{Admin}users/novo-id");

        var id = await Cliente.CriarAsync(
            new DadosNovoUsuario("maria", "Maria", "Silva", "m@e.com", "cliente-interno", "Temp@1234"), CancellationToken.None);

        Assert.Equal("novo-id", id);
        var corpo = JsonNode.Parse(_keycloak.Requisicoes.Single(r => r.Metodo == "POST" && r.Caminho == "users").Corpo)!;
        Assert.Equal("maria", (string?)corpo["username"]);
        Assert.True((bool?)corpo["enabled"]);
        Assert.Equal("password", (string?)corpo["credentials"]![0]!["type"]);
        Assert.Equal("Temp@1234", (string?)corpo["credentials"]![0]!["value"]);
        Assert.True((bool?)corpo["credentials"]![0]!["temporary"]);
        Assert.Null(corpo["id"]);
    }

    [Fact]
    public async Task Conflito_no_keycloak_vira_usuario_duplicado()
    {
        _keycloak.Responder("POST", "users", """{"errorMessage":"User exists with same username"}""", HttpStatusCode.Conflict);

        await Assert.ThrowsAsync<UsuarioDuplicadoException>(() => Cliente.CriarAsync(
            new DadosNovoUsuario("maria", "Maria", "Silva", "m@e.com", "admin", "Temp@1234"), CancellationToken.None));
    }

    [Fact]
    public async Task Atualizar_envia_so_os_campos_editaveis()
    {
        _keycloak.Responder("PUT", "users/u1", string.Empty, HttpStatusCode.NoContent);

        await Cliente.AtualizarAsync("u1", "Maria", "Souza", "m@e.com", false, CancellationToken.None);

        var corpo = JsonNode.Parse(_keycloak.Requisicoes.Single(r => r.Metodo == "PUT").Corpo)!.AsObject();
        Assert.Equal(["email", "enabled", "firstName", "lastName"], corpo.Select(p => p.Key).Order());
        Assert.False((bool?)corpo["enabled"]);
    }

    [Fact]
    public async Task Definir_papel_troca_o_papel_do_sistema_e_preserva_os_demais()
    {
        const string Mapeamentos = "users/u1/role-mappings/realm";
        _keycloak.Responder("GET", Mapeamentos,
            """[{"id":"r0","name":"default-roles-precificacao"},{"id":"r2","name":"cliente-interno"}]""");
        _keycloak.Responder("DELETE", Mapeamentos, string.Empty, HttpStatusCode.NoContent);
        _keycloak.Responder("GET", $"{Mapeamentos}/available", """[{"id":"r1","name":"admin"},{"id":"r3","name":"cliente-externo"}]""");
        _keycloak.Responder("POST", Mapeamentos, string.Empty, HttpStatusCode.NoContent);

        await Cliente.DefinirPapelAsync("u1", "admin", CancellationToken.None);

        var removidos = JsonNode.Parse(_keycloak.Requisicoes.Single(r => r.Metodo == "DELETE").Corpo)!.AsArray();
        Assert.Equal(["cliente-interno"], removidos.Select(p => (string?)p!["name"]));
        var adicionados = JsonNode.Parse(_keycloak.Requisicoes.Single(r => r.Metodo == "POST" && r.Caminho == Mapeamentos).Corpo)!.AsArray();
        Assert.Equal("r1", (string?)Assert.Single(adicionados)!["id"]);
    }

    [Fact]
    public async Task Definir_papel_que_o_usuario_ja_tem_nao_altera_nada()
    {
        _keycloak.Responder("GET", "users/u1/role-mappings/realm", """[{"id":"r1","name":"admin"}]""");

        await Cliente.DefinirPapelAsync("u1", "admin", CancellationToken.None);

        Assert.DoesNotContain(_keycloak.Requisicoes, r => r.Metodo is "POST" or "DELETE" && r.Caminho != "token");
    }

    private sealed record Requisicao(string Metodo, string Caminho, string? Autorizacao, string Corpo);

    /// <summary>Keycloak em memória: responde por método + caminho relativo à Admin API.</summary>
    private sealed class KeycloakFalso : HttpMessageHandler
    {
        private readonly Dictionary<string, (string Corpo, HttpStatusCode Status, string? Location)> _respostas = [];
        private int _tokens;

        public List<Requisicao> Requisicoes { get; } = [];

        public void Responder(string metodo, string caminho, string corpo, HttpStatusCode status = HttpStatusCode.OK, string? location = null) =>
            _respostas[$"{metodo} {caminho}"] = (corpo, status, location);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var caminho = url.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal) ? "token" : url.Replace(Admin, string.Empty);
            var corpo = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requisicoes.Add(new Requisicao(request.Method.Method, caminho, request.Headers.Authorization?.ToString(), corpo));

            if (caminho == "token")
            {
                return Json($$"""{"access_token":"tk-{{++_tokens}}","expires_in":300}""", HttpStatusCode.OK);
            }

            if (!_respostas.TryGetValue($"{request.Method.Method} {caminho}", out var resposta))
            {
                return caminho.StartsWith("users?", StringComparison.Ordinal)
                    ? Json("[]", HttpStatusCode.OK)
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var mensagem = Json(resposta.Corpo, resposta.Status);
            if (resposta.Location is not null)
            {
                mensagem.Headers.Location = new Uri(resposta.Location);
            }

            return mensagem;
        }

        private static HttpResponseMessage Json(string corpo, HttpStatusCode status) =>
            new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
    }
}
