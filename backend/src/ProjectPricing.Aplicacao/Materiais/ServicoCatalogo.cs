using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Aplicacao.Materiais;

/// <summary>Casos de uso do catálogo de materiais (RF02, RN04, RN10).</summary>
public sealed class ServicoCatalogo(
    IMaterialRepository repositorio,
    IUsuarioAtual usuario,
    TimeProvider relogio)
{
    private const string Recurso = "Material";

    public async Task<Material> CriarAsync(DadosMaterial dados, CancellationToken cancellationToken)
    {
        await GarantirNomeLivreAsync(dados, ignorarId: null, cancellationToken);

        var material = new Material(
            dados.Nome, dados.Sinonimos, dados.Tipo, dados.Categoria,
            dados.Unidade, dados.PrecoUnitario, dados.Fornecedor, relogio.GetUtcNow());
        await repositorio.InserirAsync(material, cancellationToken);
        return material;
    }

    /// <summary>Altera os dados e, pelo <paramref name="status"/>, inativa ou reativa o material.</summary>
    public async Task<Material> AlterarAsync(
        string id,
        DadosMaterial dados,
        StatusMaterial status,
        CancellationToken cancellationToken)
    {
        var material = await ObterExistenteAsync(id, cancellationToken);
        await GarantirNomeLivreAsync(dados, ignorarId: id, cancellationToken);

        var agora = relogio.GetUtcNow();
        material.Alterar(
            dados.Nome, dados.Sinonimos, dados.Tipo, dados.Categoria,
            dados.Unidade, dados.PrecoUnitario, dados.Fornecedor, agora);
        if (status == StatusMaterial.Inativo)
        {
            material.Inativar(agora);
        }
        else
        {
            material.Reativar(agora);
        }

        await repositorio.AtualizarAsync(material, cancellationToken);
        return material;
    }

    /// <summary>Exclusão lógica (RN04): o material sai do catálogo e continua no histórico.</summary>
    public async Task InativarAsync(string id, CancellationToken cancellationToken)
    {
        var material = await ObterExistenteAsync(id, cancellationToken);
        material.Inativar(relogio.GetUtcNow());
        await repositorio.AtualizarAsync(material, cancellationToken);
    }

    /// <summary>Clientes só enxergam o catálogo ativo; o Admin vê também os inativos.</summary>
    public async Task<Material> ObterAsync(string id, CancellationToken cancellationToken)
    {
        var material = await ObterExistenteAsync(id, cancellationToken);
        if (!usuario.EhAdmin && material.Status != StatusMaterial.Ativo)
        {
            throw new RecursoNaoEncontradoException(Recurso, id);
        }

        return material;
    }

    public Task<PaginaDeMateriais> ListarAsync(FiltroMateriais filtro, CancellationToken cancellationToken)
    {
        var filtroPermitido = usuario.EhAdmin ? filtro : filtro with { Status = StatusMaterial.Ativo };
        return repositorio.ListarAsync(filtroPermitido, cancellationToken);
    }

    private async Task<Material> ObterExistenteAsync(string id, CancellationToken cancellationToken)
    {
        return await repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException(Recurso, id);
    }

    /// <summary>RN10: nome e sinônimos não podem repetir nome ou sinônimo de outro material, inclusive inativo.</summary>
    private async Task GarantirNomeLivreAsync(DadosMaterial dados, string? ignorarId, CancellationToken cancellationToken)
    {
        string[] termos = [dados.Nome, .. dados.Sinonimos];
        var conflito = await repositorio.BuscarConflitoDeNomeAsync(termos, ignorarId, cancellationToken);
        if (conflito is null)
        {
            return;
        }

        var termoRepetido = termos.FirstOrDefault(termo =>
            conflito.NomeESinonimos.Any(existente => NormalizadorTexto.SaoEquivalentes(termo, existente)));
        throw new MaterialDuplicadoException(termoRepetido ?? dados.Nome);
    }
}
