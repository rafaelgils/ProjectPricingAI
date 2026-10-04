using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Precificacao;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Cotacao;

/// <summary>Material e medida que o agente quer cotar.</summary>
public sealed record ItemPedido(string MaterialId, MedidaInformada Medida);

/// <summary>
/// Tools do agente (ADR-006). O Servidor MCP as expõe em /mcp; o agente executa as mesmas implementações
/// dentro do processo. O LLM só recebe <see cref="BuscarMateriaisAsync"/>; calcular e salvar são chamadas pelo código.
/// </summary>
public interface IFerramentasCotacao
{
    /// <summary>buscarMateriais: passo 1 da RN09 para cada termo, sobre o catálogo ativo.</summary>
    Task<IReadOnlyList<ResultadoTermo>> BuscarMateriaisAsync(IReadOnlyList<string> termos, CancellationToken cancellationToken);

    /// <summary>Catálogo ativo, para o passo 2 da RN09 (sugestão do agente).</summary>
    Task<IReadOnlyList<Material>> ListarCatalogoAtivoAsync(CancellationToken cancellationToken);

    /// <summary>calcular: valores dos itens sem gravar (RN01, RN02, RN05, RN08).</summary>
    Task<ResultadoCalculo> CalcularAsync(string projetoId, IReadOnlyList<ItemPedido> itens, CancellationToken cancellationToken);

    /// <summary>salvarProjeto: recalcula e grava a cotação com o snapshot dos preços (ADR-007).</summary>
    /// <param name="versaoEsperada">
    /// Versão do projeto quando a rodada começou. Se o projeto mudou depois disso, a gravação é recusada.
    /// </param>
    /// <exception cref="ConflitoDeEdicaoException">O projeto mudou durante a rodada.</exception>
    Task<Projeto> SalvarProjetoAsync(
        string projetoId,
        IReadOnlyList<ItemPedido> itens,
        int? versaoEsperada,
        CancellationToken cancellationToken);
}

public sealed class FerramentasCotacao(
    IMaterialRepository materiais,
    IProjetoRepository projetos,
    IServicoPrecificacao precificacao,
    IUsuarioAtual usuario,
    TimeProvider relogio) : IFerramentasCotacao
{
    private const string RecursoProjeto = "Projeto";

    public async Task<IReadOnlyList<ResultadoTermo>> BuscarMateriaisAsync(
        IReadOnlyList<string> termos,
        CancellationToken cancellationToken)
    {
        var catalogo = await materiais.ListarAtivosAsync(cancellationToken);
        return [.. termos.Select(termo => ClassificadorTermos.Classificar(termo, catalogo))];
    }

    public Task<IReadOnlyList<Material>> ListarCatalogoAtivoAsync(CancellationToken cancellationToken) =>
        materiais.ListarAtivosAsync(cancellationToken);

    public async Task<ResultadoCalculo> CalcularAsync(
        string projetoId,
        IReadOnlyList<ItemPedido> itens,
        CancellationToken cancellationToken)
    {
        var projeto = await ObterProjetoDoUsuarioAsync(projetoId, cancellationToken);
        return await CalcularParaAsync(projeto, itens, cancellationToken);
    }

    public async Task<Projeto> SalvarProjetoAsync(
        string projetoId,
        IReadOnlyList<ItemPedido> itens,
        int? versaoEsperada,
        CancellationToken cancellationToken)
    {
        var projeto = await ObterProjetoDoUsuarioAsync(projetoId, cancellationToken);
        if (versaoEsperada is { } versao && projeto.Versao != versao)
        {
            throw new ConflitoDeEdicaoException();
        }

        // Recalcula aqui mesmo: o valor gravado nunca vem de fora do Serviço de Precificação (ADR-001).
        var resultado = await CalcularParaAsync(projeto, itens, cancellationToken);
        var itensProjeto = resultado.Itens
            .Select(i => new ItemProjeto(i.MaterialId, i.Nome, i.Quantidade, i.Unidade, i.PrecoUnitario, i.Subtotal))
            .ToList();
        projeto.RegistrarCotacao(itensProjeto, resultado.Total, relogio.GetUtcNow());
        await projetos.AtualizarAsync(projeto, cancellationToken);
        return projeto;
    }

    /// <summary>RN07: só o dono altera o projeto; projeto de outro cliente responde como inexistente.</summary>
    private async Task<Projeto> ObterProjetoDoUsuarioAsync(string projetoId, CancellationToken cancellationToken)
    {
        var projeto = await projetos.ObterPorIdAsync(projetoId, cancellationToken);
        if (projeto is null || projeto.ClienteId != usuario.KeycloakId)
        {
            throw new RecursoNaoEncontradoException(RecursoProjeto, projetoId);
        }

        projeto.GarantirQueNaoEstaArquivado();
        return projeto;
    }

    /// <summary>
    /// RN02: material já cotado no projeto usa o nome e o preço congelados; material novo usa o preço atual
    /// do catálogo e precisa estar ativo.
    /// </summary>
    private async Task<ResultadoCalculo> CalcularParaAsync(
        Projeto projeto,
        IReadOnlyList<ItemPedido> itens,
        CancellationToken cancellationToken)
    {
        var novos = itens.Select(i => i.MaterialId).Where(id => projeto.ItemDoMaterial(id) is null).ToList();
        var doCatalogo = (await materiais.ObterPorIdsAsync(novos, cancellationToken))
            .Where(m => m.Status == StatusMaterial.Ativo)
            .ToDictionary(m => m.Id!);

        var aCalcular = new List<ItemACalcular>(itens.Count);
        var naoEncontrados = new List<string>();
        foreach (var item in itens)
        {
            if (projeto.ItemDoMaterial(item.MaterialId) is { } cotado)
            {
                aCalcular.Add(new ItemACalcular(cotado.MaterialId, cotado.NomeSnapshot, cotado.Unidade, cotado.PrecoUnitarioSnapshot, item.Medida));
            }
            else if (doCatalogo.TryGetValue(item.MaterialId, out var material))
            {
                aCalcular.Add(new ItemACalcular(material.Id!, material.Nome, material.Unidade, material.PrecoUnitario, item.Medida));
            }
            else
            {
                naoEncontrados.Add(item.MaterialId);
            }
        }

        if (naoEncontrados.Count > 0)
        {
            throw new ItensNaoEncontradosException(projeto.Id!, naoEncontrados);
        }

        return precificacao.Calcular(aCalcular);
    }
}
