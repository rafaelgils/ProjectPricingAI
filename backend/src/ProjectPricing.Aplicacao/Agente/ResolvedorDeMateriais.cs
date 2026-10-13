using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using ProjectPricing.Aplicacao.Cotacao;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Precificacao;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Agente;

/// <summary>Resultado da correspondência entre os termos do cliente e o catálogo (RN09).</summary>
public sealed class ResolucaoDeMateriais
{
    public List<ItemPedido> Pedidos { get; } = [];

    public List<SugestaoMaterial> Sugestoes { get; } = [];

    public List<string> NaoEncontrados { get; } = [];
}

/// <summary>Segunda etapa do agente: associa cada item interpretado a um material do catálogo (RN09).</summary>
public interface IResolvedorDeMateriais
{
    Task<ResolucaoDeMateriais> ResolverAsync(
        Projeto projeto,
        Conversa conversa,
        IReadOnlyList<ItemInterpretado> itens,
        CancellationToken cancellationToken);
}

/// <summary>
/// Passo 1 da RN09 (similaridade, feito pelo código, sem confiar no <c>materialId</c> do LLM) e passo 2
/// (sugestão do LLM pelo sentido, sempre sujeita à confirmação do cliente).
/// </summary>
public sealed class ResolvedorDeMateriais(IChatClient chat, IFerramentasCotacao ferramentas) : IResolvedorDeMateriais
{
    public async Task<ResolucaoDeMateriais> ResolverAsync(
        Projeto projeto,
        Conversa conversa,
        IReadOnlyList<ItemInterpretado> itens,
        CancellationToken cancellationToken)
    {
        var pendentes = RegistroDeSugestoes.Ler(conversa).Select(s => s.MaterialId).ToHashSet(StringComparer.Ordinal);
        var resolucao = new ResolucaoDeMateriais();
        var aBuscar = new List<ItemInterpretado>();

        foreach (var item in itens)
        {
            var aceitoDireto = item.MaterialId is { } id
                && (projeto.ItemDoMaterial(id) is not null || (item.Confirmado && pendentes.Contains(id)));
            if (aceitoDireto)
            {
                resolucao.Pedidos.Add(ParaPedido(item, item.MaterialId!));
            }
            else
            {
                aBuscar.Add(item);
            }
        }

        if (aBuscar.Count == 0)
        {
            return resolucao;
        }

        // Passo 1 da RN09: a classificação é refeita pelo código, sem confiar no materialId do LLM.
        var resultados = await ferramentas.BuscarMateriaisAsync([.. aBuscar.Select(i => i.Termo)], cancellationToken);
        var semCorrespondencia = new List<ItemInterpretado>();
        foreach (var (item, resultado) in aBuscar.Zip(resultados))
        {
            switch (resultado.Classificacao)
            {
                case ClassificacaoTermo.Encontrado:
                    resolucao.Pedidos.Add(ParaPedido(item, resultado.Material!.Id!));
                    break;
                case ClassificacaoTermo.AConfirmar:
                    resolucao.Sugestoes.Add(new SugestaoMaterial(
                        item.Termo, resultado.Material!.Id!, resultado.Material.Nome, OrigemSugestao.Similaridade, resultado.Similaridade));
                    break;
                default:
                    semCorrespondencia.Add(item);
                    break;
            }
        }

        if (semCorrespondencia.Count > 0)
        {
            await SugerirPeloSentidoAsync(semCorrespondencia, resolucao, cancellationToken);
        }

        return resolucao;
    }

    /// <summary>Passo 2 da RN09: o LLM pode sugerir um material da lista; a sugestão sempre vai para confirmação.</summary>
    private async Task SugerirPeloSentidoAsync(
        IReadOnlyList<ItemInterpretado> itens,
        ResolucaoDeMateriais resolucao,
        CancellationToken cancellationToken)
    {
        var catalogo = (await ferramentas.ListarCatalogoAtivoAsync(cancellationToken)).ToDictionary(m => m.Id!);
        var termos = itens.Select(i => i.Termo).ToList();

        var pedido = new StringBuilder("Catálogo:\n");
        foreach (var material in catalogo.Values)
        {
            pedido.AppendLine(CultureInfo.InvariantCulture,
                $"- materialId={material.Id}; nome={SegurancaDoPrompt.Linha(material.Nome)}; categoria={SegurancaDoPrompt.Linha(material.Categoria)}; unidade={CodigosEnum<UnidadeMedida>.Codigo(material.Unidade)}");
        }

        pedido.AppendLine().AppendLine("Termos sem correspondência:");
        termos.ForEach(t => pedido.AppendLine(CultureInfo.InvariantCulture, $"- {SegurancaDoPrompt.Linha(t)}"));

        List<ChatMessage> mensagens = [new(ChatRole.System, PromptsAgente.Sugestao), new(ChatRole.User, pedido.ToString())];
        var resposta = await chat.GetResponseAsync<SugestoesLlm>(mensagens, OpcoesJsonDoAgente.Padrao, cancellationToken: cancellationToken);
        RecusaDoLlm.Garantir(resposta);
        var sugeridos = resposta.TryGetResult(out var saida) ? saida.Sugestoes : [];

        foreach (var termo in termos)
        {
            var termoEnviado = SegurancaDoPrompt.Linha(termo);
            var id = sugeridos.FirstOrDefault(s => s.Termo == termoEnviado)?.MaterialId;
            if (id is not null && catalogo.TryGetValue(id, out var material))
            {
                resolucao.Sugestoes.Add(new SugestaoMaterial(termo, material.Id!, material.Nome, OrigemSugestao.Agente, null));
            }
            else
            {
                // Sem sugestão, ou um id fora da lista enviada: o termo não foi encontrado (RN03).
                resolucao.NaoEncontrados.Add(termo);
            }
        }
    }

    private static ItemPedido ParaPedido(ItemInterpretado item, string materialId) =>
        new(materialId, new MedidaInformada(item.Quantidade, item.Unidade, item.Largura, item.Altura));
}
