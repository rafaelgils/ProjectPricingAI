using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Agente;

/// <summary>Opções de JSON compartilhadas pelo agente e seus colaboradores (saídas estruturadas do LLM e registros).</summary>
internal static class OpcoesJsonDoAgente
{
    public static JsonSerializerOptions Padrao { get; } = Criar();

    private static JsonSerializerOptions Criar()
    {
        var opcoes = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions);
        opcoes.Converters.Add(new JsonStringEnumConverter());
        opcoes.MakeReadOnly();
        return opcoes;
    }
}

/// <summary>
/// Sugestões oferecidas na última pergunta de confirmação. Ficam na conversa como mensagem do papel tool
/// e voltam ao agente na rodada seguinte, quando o cliente responde.
/// </summary>
internal static class RegistroDeSugestoes
{
    public static IReadOnlyList<SugestaoMaterial> Ler(Conversa conversa)
    {
        var registro = conversa.Mensagens.LastOrDefault(m => m.Papel == PapelMensagem.Tool);
        return registro is null
            ? []
            : JsonSerializer.Deserialize<Registro>(registro.Conteudo, OpcoesJsonDoAgente.Padrao)?.Sugestoes ?? [];
    }

    public static string Serializar(IReadOnlyList<SugestaoMaterial> sugestoes) =>
        JsonSerializer.Serialize(new Registro(sugestoes), OpcoesJsonDoAgente.Padrao);

    private sealed record Registro(IReadOnlyList<SugestaoMaterial> Sugestoes);
}
