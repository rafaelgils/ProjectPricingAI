using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProjectPricing.Aplicacao.Agente;
using ProjectPricing.Aplicacao.Cotacao;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Precificacao;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Tests.Regressao;

/// <summary>
/// Suíte de regressão (plano, F11; standards.md §7): as 30 descrições de referência passam pelo agente real,
/// pelas tools reais, pela RN09 real e pelo Serviço de Precificação real. Só o LLM é simulado, com respostas
/// fixas, e os repositórios ficam em memória. Os valores esperados vêm de scripts/regressao/calcular-esperados.mjs.
/// </summary>
public class SuiteRegressaoTests
{
    private const string ClienteId = "cliente-regressao";
    private const string ProjetoId = "6703f1c2a9000000000000aa";
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);
    private static readonly string Pasta = Path.Combine(AppContext.BaseDirectory, "Regressao");
    private static readonly JsonArray Casos = (JsonArray)JsonNode.Parse(File.ReadAllText(Path.Combine(Pasta, "casos-referencia.json")))!["casos"]!;

    public static TheoryData<string> IdsDosCasos() => [.. Casos.Select(c => (string)c!["id"]!)];

    [Fact]
    public void Ha_30_casos_de_referencia()
    {
        Assert.Equal(30, Casos.Count);
        Assert.Equal(30, Casos.Select(c => (string)c!["id"]!).Distinct().Count());
    }

    [Fact]
    public void Catalogo_de_referencia_respeita_a_RN10()
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Pasta, "catalogo-referencia.json")))!;
        var termos = json["materiais"]!.AsArray()
            .SelectMany(m => new[] { (string)m!["nome"]! }.Concat(m["sinonimos"]!.AsArray().Select(t => (string)t!)))
            .Select(NormalizadorTexto.Normalizar)
            .ToList();

        Assert.Equal(termos.Count, termos.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(IdsDosCasos))]
    public async Task Caso_de_referencia(string id)
    {
        var caso = Casos.Single(c => (string)c!["id"]! == id)!;
        var cenario = new Cenario();

        foreach (var (rodada, numero) in caso["rodadas"]!.AsArray().Select((r, i) => (r!, i + 1)))
        {
            var contexto = $"{id}, rodada {numero}";
            cenario.AtualizarPrecos(rodada["precosAtualizados"]?.AsObject());
            cenario.LlmResponde(rodada);
            var totalAntes = cenario.Projeto.ValorTotal;

            var erro = await Record.ExceptionAsync(() => cenario.ProcessarAsync((string)rodada["mensagem"]!));

            Assert.True(cenario.RespostasPendentes == 0, $"{contexto}: o agente não consumiu todas as respostas do LLM.");
            var esperado = rodada["esperado"]!;
            var resultado = (string)esperado["resultado"]!;
            if (resultado == "cotado")
            {
                Assert.True(erro is null, $"{contexto}: esperava cotação e veio {erro?.GetType().Name}: {erro?.Message}");
                ConferirCotacao(contexto, esperado, cenario.Projeto);
                continue;
            }

            // Fora do caminho feliz a cotação anterior é mantida (business-rules.md §6).
            Assert.Equal(totalAntes, cenario.Projeto.ValorTotal);
            switch (resultado)
            {
                case "confirmar":
                    var esclarecimento = Assert.IsType<EsclarecimentoNecessarioException>(erro);
                    Assert.Equal(
                        esperado["sugestoes"]!.AsArray().Select(s => $"{s!["termo"]}→{s["material"]} ({s["origem"]})"),
                        esclarecimento.Sugestoes.Select(s => $"{s.Termo}→{s.MaterialId} ({CodigosEnum<OrigemSugestao>.Codigo(s.Origem)})"));
                    break;
                case "esclarecimento":
                    var pergunta = Assert.IsType<EsclarecimentoNecessarioException>(erro);
                    Assert.Equal((string)esperado["pergunta"]!, pergunta.Pergunta);
                    Assert.Empty(pergunta.Sugestoes);
                    break;
                case "naoEncontrado":
                    var naoEncontrados = Assert.IsType<ItensNaoEncontradosException>(erro);
                    Assert.Equal(esperado["termos"]!.AsArray().Select(t => (string)t!), naoEncontrados.ItensNaoEncontrados);
                    break;
                case "medidaInvalida":
                    Assert.IsType<MedidaInvalidaException>(erro);
                    break;
                default:
                    Assert.Fail($"{contexto}: resultado desconhecido \"{resultado}\".");
                    break;
            }
        }
    }

    private static void ConferirCotacao(string contexto, JsonNode esperado, Projeto projeto)
    {
        static string Linha(string material, decimal quantidade, decimal preco, decimal subtotal) =>
            FormattableString.Invariant($"{material}: {quantidade:0.00} x {preco:0.00} = {subtotal:0.00}");

        var esperados = esperado["itens"]!.AsArray().Select(i => Linha(
            (string)i!["material"]!, (decimal)i["quantidade"]!, (decimal)i["precoUnitario"]!, (decimal)i["subtotal"]!));
        var obtidos = projeto.Itens.Select(i => Linha(i.MaterialId, i.Quantidade, i.PrecoUnitarioSnapshot, i.Subtotal));

        Assert.Equal(esperados, obtidos);
        Assert.True((decimal)esperado["total"]! == projeto.ValorTotal, $"{contexto}: total {projeto.ValorTotal}, esperado {esperado["total"]}.");
        Assert.Equal(StatusProjeto.Cotado, projeto.Status);
    }

    /// <summary>Um projeto novo, o catálogo de referência e o agente montado como em produção, menos o LLM.</summary>
    private sealed class Cenario
    {
        private readonly Dictionary<string, Material> _catalogo = CarregarCatalogo();
        private readonly Queue<string> _respostasDoLlm = new();
        private readonly Conversa _conversa = new(ProjetoId);
        private readonly AgenteProjetos _agente;

        public Cenario()
        {
            Projeto = new Projeto(ClienteId, "Projeto de referência", Agora);
            typeof(Projeto).GetProperty(nameof(Projeto.Id))!.SetValue(Projeto, ProjetoId);

            var materiais = new Mock<IMaterialRepository>();
            materiais.Setup(m => m.ListarAtivosAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => [.. _catalogo.Values.Where(m => m.Status == StatusMaterial.Ativo)]);
            materiais.Setup(m => m.ObterPorIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) => [.. ids.Where(_catalogo.ContainsKey).Select(i => _catalogo[i])]);
            var projetos = new Mock<IProjetoRepository>();
            projetos.Setup(p => p.ObterPorIdAsync(ProjetoId, It.IsAny<CancellationToken>())).ReturnsAsync(() => Projeto);
            projetos.Setup(p => p.AtualizarAsync(It.IsAny<Projeto>(), It.IsAny<CancellationToken>()))
                .Callback<Projeto, CancellationToken>((projeto, _) => projeto.AvancarVersao());

            var chat = new Mock<IChatClient>();
            chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, _respostasDoLlm.Dequeue())) { FinishReason = ChatFinishReason.Stop });

            var relogio = new Mock<TimeProvider>();
            relogio.Setup(r => r.GetUtcNow()).Returns(Agora);
            var ferramentas = new FerramentasCotacao(
                materiais.Object,
                projetos.Object,
                new ServicoPrecificacao(ConversorUnidades.CriarPadrao()),
                Mock.Of<IUsuarioAtual>(u => u.KeycloakId == ClienteId),
                relogio.Object);
            _agente = new AgenteProjetos(chat.Object, ferramentas, Mock.Of<IConversaRepository>(), relogio.Object, NullLogger<AgenteProjetos>.Instance);
        }

        public Projeto Projeto { get; }

        public int RespostasPendentes => _respostasDoLlm.Count;

        public Task ProcessarAsync(string mensagem) => _agente.ProcessarAsync(Projeto, _conversa, mensagem, CancellationToken.None);

        /// <summary>Interpretação da mensagem e, quando o caso pede, a resposta do passo 2 da RN09.</summary>
        public void LlmResponde(JsonNode rodada)
        {
            var llm = rodada["llm"]!;
            var interpretacao = llm["pergunta"] is { } pergunta
                ? new JsonObject { ["tipo"] = "esclarecimento", ["pergunta"] = pergunta.DeepClone(), ["itens"] = new JsonArray() }
                : new JsonObject
                {
                    ["tipo"] = "itens",
                    ["pergunta"] = null,
                    ["itens"] = new JsonArray([.. llm["itens"]!.AsArray().Select(i => (JsonNode)new JsonObject
                    {
                        ["termo"] = i!["termo"]!.DeepClone(),
                        ["materialId"] = i["materialId"]?.DeepClone(),
                        ["confirmado"] = i["confirmado"]?.DeepClone() ?? false,
                        ["quantidade"] = i["quantidade"]?.DeepClone(),
                        ["unidade"] = i["unidade"]!.DeepClone(),
                        ["largura"] = i["largura"]?.DeepClone(),
                        ["altura"] = i["altura"]?.DeepClone(),
                    })]),
                };
            _respostasDoLlm.Enqueue(interpretacao.ToJsonString());

            if (rodada["sugestoes"] is { } sugestoes)
            {
                _respostasDoLlm.Enqueue(new JsonObject { ["sugestoes"] = sugestoes.DeepClone() }.ToJsonString());
            }
        }

        /// <summary>O Admin altera preços do catálogo entre duas rodadas (RN02).</summary>
        public void AtualizarPrecos(JsonObject? precos)
        {
            foreach (var (chave, preco) in precos ?? [])
            {
                var m = _catalogo[chave];
                m.Alterar(m.Nome, m.Sinonimos, m.Tipo, m.Categoria, m.Unidade, (decimal)preco!, m.Fornecedor, Agora);
            }
        }

        private static Dictionary<string, Material> CarregarCatalogo()
        {
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Pasta, "catalogo-referencia.json")))!;
            var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            return json["materiais"]!.AsArray().Select(m =>
            {
                var material = new Material(
                    (string)m!["nome"]!,
                    m["sinonimos"]!.Deserialize<string[]>(opcoes)!,
                    DeCodigo<TipoMaterial>((string)m["tipo"]!),
                    (string)m["categoria"]!,
                    DeCodigo<UnidadeMedida>((string)m["unidade"]!),
                    (decimal)m["precoUnitario"]!,
                    (string)m["fornecedor"]!,
                    Agora);
                typeof(Material).GetProperty(nameof(Material.Id))!.SetValue(material, (string)m["chave"]!);
                return material;
            }).ToDictionary(m => m.Id!);
        }

        private static T DeCodigo<T>(string codigo)
            where T : struct, Enum =>
            CodigosEnum<T>.TentarConverter(codigo, out var valor) ? valor : throw new InvalidDataException($"Código inválido: {codigo}");
    }
}
