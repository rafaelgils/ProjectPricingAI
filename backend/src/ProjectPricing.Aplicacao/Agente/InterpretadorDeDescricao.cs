using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using ProjectPricing.Aplicacao.Cotacao;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Agente;

/// <summary>Primeira etapa do agente: transforma a conversa em itens e medidas, em saída estruturada (ADR-001).</summary>
public interface IInterpretadorDeDescricao
{
    Task<InterpretacaoLlm> InterpretarAsync(Projeto projeto, Conversa conversa, CancellationToken cancellationToken);
}

/// <summary>
/// Monta o prompt (instruções, itens atuais e sugestões pendentes), oferece ao LLM apenas a tool
/// <c>buscarMateriais</c> (ADR-006) e valida a saída por schema, com uma nova tentativa se ela vier fora dele.
/// </summary>
public sealed class InterpretadorDeDescricao(IChatClient chat, IFerramentasCotacao ferramentas) : IInterpretadorDeDescricao
{
    /// <summary>Uma nova tentativa quando a saída do LLM não segue o schema (plano, F6).</summary>
    private const int TentativasDeInterpretacao = 2;

    public async Task<InterpretacaoLlm> InterpretarAsync(Projeto projeto, Conversa conversa, CancellationToken cancellationToken)
    {
        List<ChatMessage> mensagens =
        [
            new(ChatRole.System, PromptsAgente.Interpretacao + DescreverContexto(projeto, conversa)),
            .. conversa.Mensagens
                .Where(m => m.Papel != PapelMensagem.Tool)
                .Select(m => new ChatMessage(m.Papel == PapelMensagem.Usuario ? ChatRole.User : ChatRole.Assistant, m.Conteudo)),
        ];
        var opcoes = new ChatOptions { Tools = [CriarFuncaoBuscarMateriais(cancellationToken)] };

        for (var tentativa = 1; ; tentativa++)
        {
            var resposta = await chat.GetResponseAsync<InterpretacaoLlm>(
                mensagens, OpcoesJsonDoAgente.Padrao, opcoes, cancellationToken: cancellationToken);
            RecusaDoLlm.Garantir(resposta);
            if (resposta.TryGetResult(out var interpretacao) && EhValida(interpretacao))
            {
                return interpretacao;
            }

            if (tentativa == TentativasDeInterpretacao)
            {
                throw new InvalidOperationException("O LLM devolveu uma interpretação fora do schema duas vezes.");
            }
        }
    }

    private AIFunction CriarFuncaoBuscarMateriais(CancellationToken cancellationToken)
    {
        return AIFunctionFactory.Create(
            async ([Description("Termos do cliente, um por item do projeto.")] string[] termos) =>
            {
                var resultados = await ferramentas.BuscarMateriaisAsync(termos, cancellationToken);
                return resultados.Select(ResultadoBuscaDto.De).ToList();
            },
            new AIFunctionFactoryOptions
            {
                Name = NomesFerramentas.BuscarMateriais,
                Description = "Procura no catálogo ativo o material mais próximo de cada termo e informa a classificação: " +
                              "encontrado, aConfirmar ou semCorrespondencia.",
                SerializerOptions = OpcoesJsonDoAgente.Padrao,
            });
    }

    private static string DescreverContexto(Projeto projeto, Conversa conversa)
    {
        var contexto = new StringBuilder();
        if (projeto.Itens.Count > 0)
        {
            contexto.AppendLine().AppendLine().AppendLine("## Itens atuais do projeto");
            foreach (var item in projeto.Itens)
            {
                contexto.AppendLine(CultureInfo.InvariantCulture,
                    $"- materialId={item.MaterialId}; nome={SegurancaDoPrompt.Linha(item.NomeSnapshot)}; quantidade={item.Quantidade}; unidade={CodigosEnum<UnidadeMedida>.Codigo(item.Unidade)}");
            }
        }

        var pendentes = RegistroDeSugestoes.Ler(conversa);
        if (pendentes.Count > 0)
        {
            contexto.AppendLine().AppendLine().AppendLine("## Sugestões aguardando confirmação do cliente");
            foreach (var sugestao in pendentes)
            {
                contexto.AppendLine(CultureInfo.InvariantCulture,
                    $"- termo \"{SegurancaDoPrompt.Linha(sugestao.Termo)}\" → materialId={sugestao.MaterialId}; nome={SegurancaDoPrompt.Linha(sugestao.Nome)}");
            }
        }

        return contexto.ToString();
    }

    private static bool EhValida(InterpretacaoLlm interpretacao) => interpretacao.Tipo switch
    {
        TipoInterpretacao.Esclarecimento => !string.IsNullOrWhiteSpace(interpretacao.Pergunta),
        _ => interpretacao.Itens is { Count: > 0 }
             && interpretacao.Itens.All(i => !string.IsNullOrWhiteSpace(i.Termo) && !string.IsNullOrWhiteSpace(i.Unidade))
             && SegurancaDoPrompt.DentroDosLimites(interpretacao),
    };
}

/// <summary>Recusa dos classificadores de segurança (stop_reason refusal) é falha de processamento (RN12).</summary>
internal static class RecusaDoLlm
{
    public static void Garantir(ChatResponse resposta)
    {
        if (resposta.FinishReason == ChatFinishReason.ContentFilter)
        {
            throw new InvalidOperationException("O LLM recusou a solicitação.");
        }
    }
}
