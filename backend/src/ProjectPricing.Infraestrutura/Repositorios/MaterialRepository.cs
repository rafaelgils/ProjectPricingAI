using MongoDB.Bson;
using MongoDB.Driver;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Infraestrutura.Mongo;

namespace ProjectPricing.Infraestrutura.Repositorios;

public sealed class MaterialRepository(IMongoCollection<Material> colecao)
    : RepositorioMongo<Material>(colecao, m => m.Id), IMaterialRepository
{
    private const string CampoSinonimos = "sinonimos";

    /// <summary>Mesma collation dos índices únicos: ignora maiúsculas e acentos (ADR-003, RN10).</summary>
    public static readonly Collation CollationPt = new("pt", strength: CollationStrength.Primary);

    private static readonly FilterDefinitionBuilder<Material> Filtro = Builders<Material>.Filter;

    public async Task<PaginaDeMateriais> ListarAsync(FiltroMateriais filtro, CancellationToken cancellationToken)
    {
        var condicao = MontarFiltro(filtro);

        var total = await Colecao.CountDocumentsAsync(condicao, cancellationToken: cancellationToken);
        var opcoes = new FindOptions<Material>
        {
            Collation = CollationPt,
            Sort = Builders<Material>.Sort.Ascending(m => m.Nome),
            Skip = (filtro.Pagina - 1) * filtro.Tamanho,
            Limit = filtro.Tamanho,
        };
        using var cursor = await Colecao.FindAsync(condicao, opcoes, cancellationToken);
        var itens = await cursor.ToListAsync(cancellationToken);

        return new PaginaDeMateriais(itens, total);
    }

    public async Task<IReadOnlyList<Material>> ListarAtivosAsync(CancellationToken cancellationToken)
    {
        using var cursor = await Colecao.FindAsync(
            Filtro.Eq(m => m.Status, StatusMaterial.Ativo), cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Material>> ObterPorIdsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        var validos = ids.Where(id => ObjectId.TryParse(id, out _)).Distinct().ToList();
        if (validos.Count == 0)
        {
            return [];
        }

        using var cursor = await Colecao.FindAsync(Filtro.In(m => m.Id, validos), cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task<Material?> BuscarConflitoDeNomeAsync(
        IReadOnlyCollection<string> termos,
        string? ignorarId,
        CancellationToken cancellationToken)
    {
        var condicao = Filtro.Or(
            Filtro.In(m => m.Nome, termos),
            Filtro.In(new StringFieldDefinition<Material, string>(CampoSinonimos), termos));
        if (ignorarId is not null)
        {
            condicao &= Filtro.Ne(m => m.Id, ignorarId);
        }

        var opcoes = new FindOptions<Material> { Collation = CollationPt, Limit = 1 };
        using var cursor = await Colecao.FindAsync(condicao, opcoes, cancellationToken);
        return await cursor.FirstOrDefaultAsync(cancellationToken);
    }

    public override async Task InserirAsync(Material material, CancellationToken cancellationToken)
    {
        try
        {
            await base.InserirAsync(material, cancellationToken);
        }
        catch (MongoWriteException erro) when (erro.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Dois cadastros simultâneos com o mesmo nome: o índice único barra o segundo.
            throw new MaterialDuplicadoException(material.Nome);
        }
    }

    public override async Task AtualizarAsync(Material material, CancellationToken cancellationToken)
    {
        try
        {
            await base.AtualizarAsync(material, cancellationToken);
        }
        catch (MongoWriteException erro) when (erro.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new MaterialDuplicadoException(material.Nome);
        }
    }

    public static FilterDefinition<Material> MontarFiltro(FiltroMateriais filtro)
    {
        var condicao = Filtro.Empty;

        if (filtro.Status is { } status)
        {
            condicao &= Filtro.Eq(m => m.Status, status);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Categoria))
        {
            condicao &= Filtro.Regex(m => m.Categoria, RegexSemAcento.Exato(filtro.Categoria));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var trecho = RegexSemAcento.Contendo(filtro.Busca);
            condicao &= Filtro.Or(
                Filtro.Regex(m => m.Nome, trecho),
                Filtro.Regex(new StringFieldDefinition<Material>(CampoSinonimos), trecho));
        }

        return condicao;
    }
}
