using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Infraestrutura.Mongo;
using ProjectPricing.Infraestrutura.Repositorios;

namespace ProjectPricing.Infraestrutura.Tests.Repositorios;

public class MaterialRepositoryTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 14, 30, 0, TimeSpan.Zero);

    public MaterialRepositoryTests()
    {
        MapeamentoMongo.Registrar();
    }

    [Fact]
    public void Filtro_vazio_traz_todo_o_catalogo()
    {
        var filtro = MaterialRepository.MontarFiltro(new FiltroMateriais(null, " ", null, 1, 20));

        Assert.Equal(new BsonDocument(), Renderizar(filtro));
    }

    [Fact]
    public void Filtro_combina_status_categoria_exata_e_busca_no_nome_ou_sinonimos()
    {
        var filtro = MaterialRepository.MontarFiltro(new FiltroMateriais("pelicula", "Sinalização", StatusMaterial.Ativo, 1, 20));

        var documento = Renderizar(filtro);

        Assert.Equal("ativo", documento["status"].AsString);
        Assert.Equal("^s[iíìîï][nñ][aáàâãä]l[iíìîï]z[aáàâãä][cç][aáàâãä][oóòôõö]$", documento["categoria"].AsBsonRegularExpression.Pattern);
        var ou = documento["$or"].AsBsonArray;
        Assert.Equal("nome", ou[0].AsBsonDocument.Names.Single());
        Assert.Equal("sinonimos", ou[1].AsBsonDocument.Names.Single());
    }

    [Fact]
    public async Task Listar_conta_o_total_e_pagina_ordenando_pelo_nome()
    {
        var material = NovoMaterial("Chapa");
        var colecao = new Mock<IMongoCollection<Material>>();
        FindOptions<Material, Material>? opcoesUsadas = null;
        colecao
            .Setup(c => c.CountDocumentsAsync(It.IsAny<FilterDefinition<Material>>(), It.IsAny<CountOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(41);
        colecao
            .Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Material>>(), It.IsAny<FindOptions<Material, Material>>(), It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<Material>, FindOptions<Material, Material>, CancellationToken>((_, opcoes, _) => opcoesUsadas = opcoes)
            .ReturnsAsync(Cursor(material));

        var pagina = await new MaterialRepository(colecao.Object)
            .ListarAsync(new FiltroMateriais(null, null, null, 3, 20), CancellationToken.None);

        Assert.Equal(41, pagina.Total);
        Assert.Equal([material], pagina.Itens);
        Assert.NotNull(opcoesUsadas);
        Assert.Equal(40, opcoesUsadas.Skip);
        Assert.Equal(20, opcoesUsadas.Limit);
        Assert.Same(MaterialRepository.CollationPt, opcoesUsadas.Collation);
    }

    [Fact]
    public async Task Conflito_de_nome_procura_em_nomes_e_sinonimos_com_collation_e_ignora_o_proprio_material()
    {
        const string id = "6703e0aa0000000000000001";
        var colecao = new Mock<IMongoCollection<Material>>();
        FilterDefinition<Material>? filtroUsado = null;
        FindOptions<Material, Material>? opcoesUsadas = null;
        colecao
            .Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Material>>(), It.IsAny<FindOptions<Material, Material>>(), It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<Material>, FindOptions<Material, Material>, CancellationToken>((filtro, opcoes, _) =>
            {
                filtroUsado = filtro;
                opcoesUsadas = opcoes;
            })
            .ReturnsAsync(Cursor<Material>());

        var conflito = await new MaterialRepository(colecao.Object)
            .BuscarConflitoDeNomeAsync(["Chapa", "placa"], id, CancellationToken.None);

        Assert.Null(conflito);
        var documento = Renderizar(filtroUsado!);
        Assert.Contains("Chapa", documento.ToString(), StringComparison.Ordinal);
        Assert.Contains("sinonimos", documento.ToString(), StringComparison.Ordinal);
        Assert.Contains(ObjectId.Parse(id).ToString(), documento.ToString(), StringComparison.Ordinal);
        Assert.Same(MaterialRepository.CollationPt, opcoesUsadas!.Collation);
        Assert.Equal(1, opcoesUsadas.Limit);
    }

    [Theory]
    [InlineData("pelicula", "Película refletiva", true)]
    [InlineData("CABEÇOTE", "Cabecote de metal", true)]
    [InlineData("a.b", "aXb", false)]
    [InlineData("tinta", "Película refletiva", false)]
    public void Regex_ignora_acento_e_maiuscula_e_escapa_caracteres_especiais(string termo, string texto, bool encontra)
    {
        var regex = RegexSemAcento.Contendo(termo);

        Assert.Equal(encontra, Regex.IsMatch(texto, regex.Pattern, RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Regex_exato_nao_aceita_trecho()
    {
        var regex = RegexSemAcento.Exato("sinalizacao");

        Assert.Matches(new Regex(regex.Pattern, RegexOptions.IgnoreCase), "Sinalização");
        Assert.DoesNotMatch(new Regex(regex.Pattern, RegexOptions.IgnoreCase), "Sinalização viária");
    }

    [Fact]
    public void Lista_de_nome_e_sinonimos_nao_e_gravada_no_documento()
    {
        Assert.False(NovoMaterial("Chapa").ToBsonDocument().Contains("nomeESinonimos"));
    }

    private static Material NovoMaterial(string nome) =>
        new(nome, [], TipoMaterial.Material, "Sinalização", UnidadeMedida.MetroQuadrado, 120m, "Aço SA", Agora);

    private static IAsyncCursor<T> Cursor<T>(params T[] documentos)
    {
        var cursor = new Mock<IAsyncCursor<T>>();
        cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(documentos.Length > 0).ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns(documentos);
        return cursor.Object;
    }

    private static BsonDocument Renderizar(FilterDefinition<Material> filtro)
    {
        var registro = BsonSerializer.SerializerRegistry;
        return filtro.Render(new RenderArgs<Material>(registro.GetSerializer<Material>(), registro));
    }
}
