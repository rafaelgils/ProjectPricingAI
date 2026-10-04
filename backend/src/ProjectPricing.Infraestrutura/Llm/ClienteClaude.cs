using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using BetaMensagens = Anthropic.Models.Beta.Messages;

namespace ProjectPricing.Infraestrutura.Llm;

/// <summary>Seção "Anthropic" da configuração (ADR-008). A chave vem de variável de ambiente, nunca do repositório.</summary>
public sealed class OpcoesAnthropic
{
    public const string Secao = "Anthropic";

    public string? ApiKey { get; init; }

    public string Modelo { get; init; } = "claude-opus-5-5";

    /// <summary>Esforço do raciocínio. No Claude Opus 5.5 o padrão da API é medium; aqui fica explícito.</summary>
    public ReasoningEffort Esforco { get; init; } = ReasoningEffort.Medium;

    public int MaxTokens { get; init; } = 16000;

    public int TimeoutSegundos { get; init; } = 90;
}

/// <summary>
/// Aplica a configuração da Anthropic a toda chamada: esforço (thinking adaptativo) e fallback do servidor
/// para recusas dos classificadores de segurança (beta server-side-fallback, fallbacks: "default").
/// </summary>
public sealed class ConfiguracaoClaude(IChatClient interno, OpcoesAnthropic opcoes) : DelegatingChatClient(interno)
{
    public const string BetaFallback = "server-side-fallback-2026-07-01";

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(messages, Configurar(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(messages, Configurar(options), cancellationToken);

    public ChatOptions Configurar(ChatOptions? options)
    {
        var configuradas = options?.Clone() ?? new ChatOptions();
        configuradas.Reasoning ??= new ReasoningOptions { Effort = opcoes.Esforco };
        configuradas.RawRepresentationFactory ??= CriarParametros;
        return configuradas;
    }

    /// <summary>Parâmetros de partida da requisição; o cliente do SDK completa mensagens, tools e formato.</summary>
    private object? CriarParametros(IChatClient _) => new BetaMensagens.MessageCreateParams
    {
        Betas = [BetaFallback],
        Fallbacks = new BetaMensagens.Default(),
        Model = opcoes.Modelo,
        MaxTokens = opcoes.MaxTokens,
        Messages = [],
    };
}

public static class InjecaoDeDependenciaLlm
{
    /// <summary>
    /// IChatClient do Claude (ADR-008): SDK oficial pelo endpoint beta (necessário para os fallbacks),
    /// com invocação automática das tools que o agente oferece ao LLM.
    /// </summary>
    public static IServiceCollection AdicionarClaude(this IServiceCollection services, OpcoesAnthropic opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        services.AddSingleton(opcoes);
        services.AddSingleton(_ => new AnthropicClient
        {
            ApiKey = string.IsNullOrWhiteSpace(opcoes.ApiKey) ? null : opcoes.ApiKey,
            Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos),
        });
        services.AddSingleton(sp => CriarPipeline(sp.GetRequiredService<AnthropicClient>(), opcoes));

        return services;
    }

    /// <summary>
    /// Invocação de tools por fora (o LLM chama buscarMateriais e recebe o resultado) e a configuração
    /// da Anthropic em cada chamada ao modelo.
    /// </summary>
    public static IChatClient CriarPipeline(AnthropicClient cliente, OpcoesAnthropic opcoes) =>
        new ChatClientBuilder(cliente.Beta.AsIChatClient(opcoes.Modelo, opcoes.MaxTokens))
            .UseFunctionInvocation()
            .Use(interno => new ConfiguracaoClaude(interno, opcoes))
            .Build();
}
