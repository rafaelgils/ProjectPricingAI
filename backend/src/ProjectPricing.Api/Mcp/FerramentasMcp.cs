using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ProjectPricing.Aplicacao.Agente;
using ProjectPricing.Aplicacao.Cotacao;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Api.Mcp;

/// <summary>
/// Servidor MCP único (ADR-006), em /mcp na rede interna. Só traduz o protocolo: as regras ficam em
/// <see cref="IFerramentasCotacao"/>, a mesma implementação que o agente executa.
/// </summary>
[McpServerToolType]
public sealed class FerramentasMcp(IFerramentasCotacao ferramentas)
{
    [McpServerTool(Name = NomesFerramentas.BuscarMateriais, ReadOnly = true)]
    [Description("Procura no catálogo ativo o material mais próximo de cada termo (RN09) e informa a classificação: encontrado, aConfirmar ou semCorrespondencia.")]
    public Task<IReadOnlyList<ResultadoBuscaDto>> BuscarMateriais(
        [Description("Termos do cliente, um por item do projeto.")] string[] termos,
        CancellationToken cancellationToken) =>
        Executar<IReadOnlyList<ResultadoBuscaDto>>(async () =>
            [.. (await ferramentas.BuscarMateriaisAsync(termos, cancellationToken)).Select(ResultadoBuscaDto.De)]);

    [McpServerTool(Name = NomesFerramentas.Calcular, ReadOnly = true)]
    [Description("Calcula os itens e o total do projeto pelo Serviço de Precificação, sem gravar (RN01, RN02, RN05, RN08).")]
    public Task<CalculoDto> Calcular(
        [Description("Id do projeto do usuário.")] string projetoId,
        [Description("Itens com material e medida.")] ItemPedidoDto[] itens,
        CancellationToken cancellationToken) =>
        Executar(async () => CalculoDto.De(
            await ferramentas.CalcularAsync(projetoId, [.. itens.Select(i => i.ParaItemPedido())], cancellationToken)));

    [McpServerTool(Name = NomesFerramentas.SalvarProjeto, Destructive = false)]
    [Description("Recalcula e grava a cotação do projeto, com o snapshot dos preços (ADR-007).")]
    public Task<CalculoDto> SalvarProjeto(
        [Description("Id do projeto do usuário.")] string projetoId,
        [Description("Itens com material e medida.")] ItemPedidoDto[] itens,
        CancellationToken cancellationToken) =>
        Executar(async () => CalculoDto.De(
            await ferramentas.SalvarProjetoAsync(projetoId, [.. itens.Select(i => i.ParaItemPedido())], versaoEsperada: null, cancellationToken)));

    /// <summary>
    /// Erros de negócio chegam ao cliente MCP com o code estável e a mensagem (standards.md §5).
    /// Os demais continuam genéricos: o SDK do MCP não expõe detalhes internos.
    /// </summary>
    private static async Task<T> Executar<T>(Func<Task<T>> acao)
    {
        try
        {
            return await acao();
        }
        catch (ExcecaoDeDominio erro)
        {
            throw new McpException($"{erro.Codigo}: {erro.Message}", erro);
        }
    }
}
