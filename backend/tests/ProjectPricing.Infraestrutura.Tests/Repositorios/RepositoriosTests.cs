using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Projetos;
using ProjectPricing.Infraestrutura.Mongo;
using ProjectPricing.Infraestrutura.Repositorios;

namespace ProjectPricing.Infraestrutura.Tests.Repositorios;

public class RepositoriosTests
{
    private const string IdValido = "6703e0aa0000000000000001";
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 14, 30, 0, TimeSpan.FromHours(-3));

    public RepositoriosTests()
    {
        MapeamentoMongo.Registrar();
    }

    [Fact]
    public async Task Inserir_envia_o_documento_para_a_colecao()
    {
        var colecao = new Mock<IMongoCollection<Material>>();
        var material = NovoMaterial();

        await new MaterialRepository(colecao.Object).InserirAsync(material, CancellationToken.None);

        colecao.Verify(c => c.InsertOneAsync(material, null, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Atualizar_projeto_grava_so_sobre_a_versao_lida_e_avanca_a_versao()
    {
        var colecao = new Mock<IMongoCollection<Projeto>>();
        FilterDefinition<Projeto>? filtroUsado = null;
        colecao
            .Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<Projeto>>(), It.IsAny<Projeto>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<Projeto>, Projeto, ReplaceOptions, CancellationToken>((filtro, _, _, _) => filtroUsado = filtro)
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
        var projeto = ProjetoComId(IdValido);

        await new ProjetoRepository(colecao.Object).AtualizarAsync(projeto, CancellationToken.None);

        Assert.NotNull(filtroUsado);
        Assert.Equal(new BsonDocument { ["_id"] = ObjectId.Parse(IdValido), ["versao"] = 0 }, Renderizar(filtroUsado));
        Assert.Equal(1, projeto.Versao);
    }

    [Fact]
    public async Task Atualizar_projeto_alterado_por_outra_gravacao_e_conflito()
    {
        var colecao = new Mock<IMongoCollection<Projeto>>();
        colecao
            .Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<Projeto>>(), It.IsAny<Projeto>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(0, 0, null));

        await Assert.ThrowsAsync<ProjectPricing.Dominio.Excecoes.ConflitoDeEdicaoException>(() =>
            new ProjetoRepository(colecao.Object).AtualizarAsync(ProjetoComId(IdValido), CancellationToken.None));
    }

    [Fact]
    public void Filtro_de_projetos_por_cliente_e_status()
    {
        var filtro = ProjetoRepository.MontarFiltro(new FiltroProjetos("kc-1", StatusProjeto.Cotado, 1, 20));

        Assert.Equal(new BsonDocument { ["clienteId"] = "kc-1", ["status"] = "cotado" }, Renderizar(filtro));
        Assert.Equal(new BsonDocument(), Renderizar(ProjetoRepository.MontarFiltro(new FiltroProjetos(null, null, 1, 20))));
    }

    [Fact]
    public async Task Listar_projetos_conta_e_pagina()
    {
        var colecao = new Mock<IMongoCollection<Projeto>>();
        FindOptions<Projeto, Projeto>? opcoesUsadas = null;
        colecao
            .Setup(c => c.CountDocumentsAsync(It.IsAny<FilterDefinition<Projeto>>(), It.IsAny<CountOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var cursor = new Mock<IAsyncCursor<Projeto>>();
        cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns([ProjetoComId(IdValido)]);
        colecao
            .Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Projeto>>(), It.IsAny<FindOptions<Projeto, Projeto>>(), It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<Projeto>, FindOptions<Projeto, Projeto>, CancellationToken>((_, opcoes, _) => opcoesUsadas = opcoes)
            .ReturnsAsync(cursor.Object);

        var pagina = await new ProjetoRepository(colecao.Object).ListarAsync(new FiltroProjetos(null, null, 2, 2), CancellationToken.None);

        Assert.Equal(3, pagina.Total);
        Assert.Single(pagina.Itens);
        Assert.Equal(2, opcoesUsadas!.Skip);
        Assert.Equal(2, opcoesUsadas.Limit);
    }

    [Fact]
    public async Task Atualizar_entidade_sem_id_e_erro_de_programacao()
    {
        var repositorio = new MaterialRepository(Mock.Of<IMongoCollection<Material>>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repositorio.AtualizarAsync(NovoMaterial(), CancellationToken.None));
    }

    [Fact]
    public async Task Obter_por_id_em_formato_invalido_retorna_nulo_sem_consultar()
    {
        var colecao = new Mock<IMongoCollection<Material>>(MockBehavior.Strict);

        var material = await new MaterialRepository(colecao.Object).ObterPorIdAsync("nao-e-objectid", CancellationToken.None);

        Assert.Null(material);
    }

    [Fact]
    public async Task Obter_por_id_retorna_o_documento_encontrado()
    {
        var esperado = NovoMaterial();
        var colecao = ColecaoQueRetorna(esperado, out var filtros);

        var material = await new MaterialRepository(colecao.Object).ObterPorIdAsync(IdValido, CancellationToken.None);

        Assert.Same(esperado, material);
        Assert.Equal(new BsonDocument("_id", ObjectId.Parse(IdValido)), Renderizar(Assert.Single(filtros)));
    }

    [Fact]
    public async Task Conversa_e_buscada_pelo_projetoId()
    {
        var esperada = new Conversa(IdValido);
        var colecao = ColecaoQueRetorna(esperada, out var filtros);

        var conversa = await new ConversaRepository(colecao.Object).ObterPorProjetoIdAsync(IdValido, CancellationToken.None);

        Assert.Same(esperada, conversa);
        Assert.Equal(new BsonDocument("projetoId", ObjectId.Parse(IdValido)), Renderizar(Assert.Single(filtros)));
    }

    private static Material NovoMaterial() =>
        new("Chapa", [], TipoMaterial.Material, "Sinalização", UnidadeMedida.MetroQuadrado, 120m, "Aço SA", Agora);

    private static Projeto ProjetoComId(string id)
    {
        var documento = new Projeto("cliente", "Placa", Agora).ToBsonDocument();
        documento["_id"] = ObjectId.Parse(id);
        return BsonSerializer.Deserialize<Projeto>(documento);
    }

    private static Mock<IMongoCollection<T>> ColecaoQueRetorna<T>(T documento, out List<FilterDefinition<T>> filtros)
    {
        var filtrosUsados = new List<FilterDefinition<T>>();
        var cursor = new Mock<IAsyncCursor<T>>();
        cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns([documento]);

        var colecao = new Mock<IMongoCollection<T>>();
        colecao
            .Setup(c => c.FindAsync(It.IsAny<FilterDefinition<T>>(), It.IsAny<FindOptions<T, T>>(), It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<T>, FindOptions<T, T>, CancellationToken>((filtro, _, _) => filtrosUsados.Add(filtro))
            .ReturnsAsync(cursor.Object);

        filtros = filtrosUsados;
        return colecao;
    }

    private static BsonDocument Renderizar<T>(FilterDefinition<T> filtro)
    {
        var registro = BsonSerializer.SerializerRegistry;
        return filtro.Render(new RenderArgs<T>(registro.GetSerializer<T>(), registro));
    }
}
