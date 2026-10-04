using System.Reflection;

namespace ProjectPricing.Aplicacao.Agente;

/// <summary>
/// Prompts versionados em backend/prompts (standards.md §6), embutidos no assembly na compilação.
/// </summary>
public static class PromptsAgente
{
    public static string Interpretacao { get; } = Ler("interpretacao.md");

    public static string Sugestao { get; } = Ler("sugestao.md");

    private static string Ler(string arquivo)
    {
        var nome = $"Prompts.{arquivo}";
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream(nome)
            ?? throw new InvalidOperationException($"Prompt {nome} não foi embutido no assembly.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }
}
