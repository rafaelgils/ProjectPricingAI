using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ProjectPricing.Infraestrutura.Llm;

namespace ProjectPricing.Infraestrutura.Tests.Llm;

/// <summary>
/// O SDK oficial monta a requisição de verdade; um HttpMessageHandler falso a captura e devolve uma resposta
/// pronta. Nenhuma chamada sai para a Claude API (standards.md §7).
/// </summary>
public class ClienteClaudeTests
{
    private readonly OpcoesAnthropic _opcoes = new() { ApiKey = "chave-de-teste" };

    [Fact]
    public async Task Requisicao_usa_o_modelo_o_esforco_o_fallback_e_a_saida_estruturada()
    {
        var capturador = new Capturador("""{"nome":"placa"}""");
        var chat = ClienteClaudeComHandler(capturador);

        var resposta = await chat.GetResponseAsync<Exemplo>([new ChatMessage(ChatRole.User, "Placa 60x60")]);

        Assert.True(resposta.TryGetResult(out var exemplo));
        Assert.Equal("placa", exemplo.Nome);

        var corpo = capturador.Corpos.Single();
        Assert.Equal("claude-opus-5-5", corpo["model"]!.GetValue<string>());
        Assert.Equal("default", corpo["fallbacks"]!.GetValue<string>());
        Assert.Equal("medium", corpo["output_config"]!["effort"]!.GetValue<string>());
        Assert.Equal("json_schema", corpo["output_config"]!["format"]!["type"]!.GetValue<string>());
        Assert.Equal("adaptive", corpo["thinking"]!["type"]!.GetValue<string>());
        Assert.Contains(ConfiguracaoClaude.BetaFallback, capturador.Betas.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tool_oferecida_pelo_agente_vai_na_requisicao_e_e_executada_pelo_pipeline()
    {
        var capturador = new Capturador(
            usoDeTool: ("buscarMateriais", """{"termos":["placa"]}"""),
            textoFinal: """{"nome":"Chapa"}""");
        var chat = ClienteClaudeComHandler(capturador);
        var termosRecebidos = new List<string>();
        var ferramenta = AIFunctionFactory.Create(
            ([Description("termos")] string[] termos) =>
            {
                termosRecebidos.AddRange(termos);
                return "Chapa de aço galvanizado";
            },
            "buscarMateriais");

        var resposta = await chat.GetResponseAsync<Exemplo>(
            [new ChatMessage(ChatRole.User, "Placa")], options: new ChatOptions { Tools = [ferramenta] });

        Assert.Equal(["placa"], termosRecebidos);
        Assert.Equal("Chapa", resposta.Result.Nome);
        Assert.Equal(2, capturador.Corpos.Count);
        Assert.Equal("buscarMateriais", capturador.Corpos[0]["tools"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("tool_result", capturador.Corpos[1]["messages"]!.AsArray().Last()!["content"]![0]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task Recusa_do_modelo_chega_como_ContentFilter()
    {
        var chat = ClienteClaudeComHandler(new Capturador("", motivoDeParada: "refusal"));

        var resposta = await chat.GetResponseAsync([new ChatMessage(ChatRole.User, "x")]);

        Assert.Equal(ChatFinishReason.ContentFilter, resposta.FinishReason);
    }

    [Fact]
    public void Esforco_e_fabrica_informados_pelo_chamador_sao_mantidos()
    {
        var configuracao = new ConfiguracaoClaude(new Moq.Mock<IChatClient>().Object, _opcoes);
        object? Fabrica(IChatClient _) => null;

        var opcoes = configuracao.Configurar(new ChatOptions
        {
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High },
            RawRepresentationFactory = Fabrica,
        });

        Assert.Equal(ReasoningEffort.High, opcoes.Reasoning!.Effort);
        Assert.Equal(Fabrica, opcoes.RawRepresentationFactory);
    }

    [Fact]
    public void Registra_o_IChatClient_e_as_opcoes()
    {
        var services = new ServiceCollection().AdicionarClaude(_opcoes);

        Assert.Contains(services, s => s.ServiceType == typeof(IChatClient));
        Assert.Contains(services, s => s.ServiceType == typeof(AnthropicClient));
        Assert.Contains(services, s => s.ServiceType == typeof(OpcoesAnthropic));
    }

    private IChatClient ClienteClaudeComHandler(HttpMessageHandler handler)
    {
        var cliente = new AnthropicClient { ApiKey = _opcoes.ApiKey, HttpClient = new HttpClient(handler), MaxRetries = 0 };
        return InjecaoDeDependenciaLlm.CriarPipeline(cliente, _opcoes);
    }

    public sealed record Exemplo(string Nome);

    /// <summary>Responde como a Messages API e guarda o corpo e os betas de cada requisição.</summary>
    private sealed class Capturador(
        string textoFinal,
        string motivoDeParada = "end_turn",
        (string Nome, string Entrada)? usoDeTool = null) : HttpMessageHandler
    {
        public Capturador((string Nome, string Entrada) usoDeTool, string textoFinal)
            : this(textoFinal, "end_turn", usoDeTool)
        {
        }

        public List<JsonNode> Corpos { get; } = [];

        public List<string> Betas { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Corpos.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!);
            Betas.Add(request.Headers.TryGetValues("anthropic-beta", out var valores) ? string.Join(",", valores) : "");

            var primeiraChamadaComTool = usoDeTool is not null && Corpos.Count == 1;
            object conteudo = primeiraChamadaComTool
                ? new { type = "tool_use", id = "toolu_1", name = usoDeTool!.Value.Nome, input = JsonNode.Parse(usoDeTool.Value.Entrada) }
                : new { type = "text", text = textoFinal };
            var corpo = JsonSerializer.Serialize(new
            {
                id = $"msg_{Corpos.Count}",
                type = "message",
                role = "assistant",
                model = "claude-opus-5-5",
                content = new[] { conteudo },
                stop_reason = primeiraChamadaComTool ? "tool_use" : motivoDeParada,
                stop_sequence = (string?)null,
                usage = new { input_tokens = 10, output_tokens = 5 },
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
            };
        }
    }
}
