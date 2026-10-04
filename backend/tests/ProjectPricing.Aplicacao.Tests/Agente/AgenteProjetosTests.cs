using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProjectPricing.Aplicacao.Agente;
using ProjectPricing.Aplicacao.Cotacao;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Precificacao;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Tests.Agente;

/// <summary>LLM simulado com respostas fixas (standards.md §7); nenhuma chamada à Claude API.</summary>
public class AgenteProjetosTests
{
    private const string ProjetoId = "6703f1c2a900000000000001";
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);

    private static readonly Material Chapa = MaterialComId("m-chapa", "Chapa de aço galvanizado", ["placa"], UnidadeMedida.MetroQuadrado, 120m);
    private static readonly Material Pelicula = MaterialComId("m-pelicula", "Película refletiva", ["tinta reflexiva"], UnidadeMedida.MetroQuadrado, 95m);
    private static readonly Material Suporte = MaterialComId("m-suporte", "Suporte em L de aço", [], UnidadeMedida.Unidade, 12m);

    private readonly Mock<IChatClient> _chat = new();
    private readonly Mock<IFerramentasCotacao> _ferramentas = new();
    private readonly Mock<IConversaRepository> _conversas = new();
    private readonly Queue<string> _respostasDoLlm = new();
    private readonly List<ChatOptions?> _opcoesRecebidas = [];
    private readonly Projeto _projeto = ProjetoComId();
    private readonly Conversa _conversa = new(ProjetoId);

    public AgenteProjetosTests()
    {
        _chat
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, opcoes, _) => _opcoesRecebidas.Add(opcoes))
            .ReturnsAsync(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, _respostasDoLlm.Dequeue())) { FinishReason = ChatFinishReason.Stop });
        _ferramentas
            .Setup(f => f.CalcularAsync(ProjetoId, It.IsAny<IReadOnlyList<ItemPedido>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoCalculo([], 105.90m));
        _ferramentas
            .Setup(f => f.SalvarProjetoAsync(ProjetoId, It.IsAny<IReadOnlyList<ItemPedido>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_projeto);
        _ferramentas
            .Setup(f => f.ListarCatalogoAtivoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Chapa, Pelicula, Suporte]);
    }

    [Fact]
    public async Task Fluxo_principal_calcula_salva_e_responde_com_o_valor()
    {
        LlmResponde(Interpretacao(Item("placa", "cm", largura: 60, altura: 60), Item("tinta reflexiva", "cm", largura: 60, altura: 60)));
        CatalogoClassifica(("placa", Chapa, 1m, ClassificacaoTermo.Encontrado), ("tinta reflexiva", Pelicula, 1m, ClassificacaoTermo.Encontrado));

        var resultado = await Processar("Placa 60x60 com tinta reflexiva");

        Assert.Same(_projeto, resultado.Projeto);
        Assert.Equal("Para esse projeto o valor estimado é R$ 105,90.", resultado.Mensagem);
        _ferramentas.Verify(f => f.SalvarProjetoAsync(ProjetoId,
            It.Is<IReadOnlyList<ItemPedido>>(p => p.Select(i => i.MaterialId).SequenceEqual(new[] { "m-chapa", "m-pelicula" })
                && p[0].Medida == new MedidaInformada(null, "cm", 60, 60)), It.IsAny<int?>(), It.IsAny<CancellationToken>()));
        Assert.Equal([PapelMensagem.Usuario, PapelMensagem.Assistente], _conversa.Mensagens.Select(m => m.Papel));
        _conversas.Verify(c => c.AtualizarAsync(_conversa, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task So_buscarMateriais_e_oferecida_ao_LLM()
    {
        LlmResponde(Interpretacao(Item("placa", "m2", quantidade: 1)));
        CatalogoClassifica(("placa", Chapa, 1m, ClassificacaoTermo.Encontrado));

        await Processar("Placa de 1 m²");

        var ferramenta = Assert.Single(_opcoesRecebidas.Single()!.Tools!);
        Assert.Equal("buscarMateriais", ferramenta.Name);
    }

    [Fact]
    public async Task Termo_sem_correspondencia_e_sem_sugestao_vira_item_nao_encontrado()
    {
        LlmResponde(
            Interpretacao(Item("placa", "un", quantidade: 1), Item("cabeçote de metal", "un", quantidade: 1)),
            """{"sugestoes":[{"termo":"cabeçote de metal","materialId":null}]}""");
        CatalogoClassifica(("placa", Chapa, 1m, ClassificacaoTermo.Encontrado), ("cabeçote de metal", Suporte, 0.3m, ClassificacaoTermo.SemCorrespondencia));

        var erro = await Assert.ThrowsAsync<ItensNaoEncontradosException>(() => Processar("Placa com cabeçote de metal"));

        Assert.Equal(["cabeçote de metal"], erro.ItensNaoEncontrados);
        Assert.Equal(ProjetoId, erro.ProjetoId);
        _ferramentas.Verify(f => f.CalcularAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ItemPedido>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("cabeçote de metal", _conversa.Mensagens.Last().Conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Similaridade_incerta_pede_confirmacao_e_a_confirmacao_cota()
    {
        LlmResponde(Interpretacao(Item("película reflexiva", "m2", quantidade: 1)));
        CatalogoClassifica(("película reflexiva", Pelicula, 0.94m, ClassificacaoTermo.AConfirmar));

        var erro = await Assert.ThrowsAsync<EsclarecimentoNecessarioException>(() => Processar("1 m² de película reflexiva"));

        var sugestao = Assert.Single(erro.Sugestoes);
        Assert.Equal(new SugestaoMaterial("película reflexiva", "m-pelicula", "Película refletiva", OrigemSugestao.Similaridade, 0.94m), sugestao);
        Assert.Contains("Película refletiva", erro.Pergunta, StringComparison.Ordinal);
        Assert.Equal(PapelMensagem.Tool, _conversa.Mensagens[1].Papel);

        // Segunda rodada: o cliente confirma e o LLM devolve o material sugerido com confirmado = true.
        LlmResponde(Interpretacao(Item("película reflexiva", "m2", quantidade: 1, materialId: "m-pelicula", confirmado: true)));

        await Processar("Sim, pode usar a película refletiva");

        _ferramentas.Verify(f => f.SalvarProjetoAsync(ProjetoId,
            It.Is<IReadOnlyList<ItemPedido>>(p => p.Single().MaterialId == "m-pelicula"), It.IsAny<int?>(), It.IsAny<CancellationToken>()));
        _ferramentas.Verify(f => f.BuscarMateriaisAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sugestao_do_agente_pelo_sentido_tambem_exige_confirmacao()
    {
        LlmResponde(
            Interpretacao(Item("cantoneira", "un", quantidade: 4)),
            """{"sugestoes":[{"termo":"cantoneira","materialId":"m-suporte"}]}""");
        CatalogoClassifica(("cantoneira", Suporte, 0.2m, ClassificacaoTermo.SemCorrespondencia));

        var erro = await Assert.ThrowsAsync<EsclarecimentoNecessarioException>(() => Processar("4 cantoneiras"));

        var sugestao = Assert.Single(erro.Sugestoes);
        Assert.Equal(OrigemSugestao.Agente, sugestao.Origem);
        Assert.Null(sugestao.Similaridade);
        Assert.Contains("sugerido pelo assistente", erro.Pergunta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sugestao_do_agente_com_id_fora_do_catalogo_e_descartada()
    {
        LlmResponde(
            Interpretacao(Item("cantoneira", "un", quantidade: 4)),
            """{"sugestoes":[{"termo":"cantoneira","materialId":"id-inventado"}]}""");
        CatalogoClassifica(("cantoneira", Suporte, 0.2m, ClassificacaoTermo.SemCorrespondencia));

        var erro = await Assert.ThrowsAsync<ItensNaoEncontradosException>(() => Processar("4 cantoneiras"));

        Assert.Equal(["cantoneira"], erro.ItensNaoEncontrados);
    }

    [Fact]
    public async Task Item_nao_encontrado_prevalece_sobre_item_a_confirmar()
    {
        LlmResponde(
            Interpretacao(Item("película reflexiva", "m2", quantidade: 1), Item("parafuso", "un", quantidade: 4)),
            """{"sugestoes":[{"termo":"parafuso","materialId":null}]}""");
        CatalogoClassifica(
            ("película reflexiva", Pelicula, 0.94m, ClassificacaoTermo.AConfirmar),
            ("parafuso", Suporte, 0.1m, ClassificacaoTermo.SemCorrespondencia));

        await Assert.ThrowsAsync<ItensNaoEncontradosException>(() => Processar("película reflexiva e parafusos"));
    }

    [Fact]
    public async Task Medida_faltando_vira_pergunta_ao_cliente()
    {
        LlmResponde("""{"tipo":"esclarecimento","pergunta":"Qual o tamanho da placa?","itens":[{"termo":"placa","materialId":null,"confirmado":false,"quantidade":null,"unidade":"cm","largura":null,"altura":null}]}""");
        CatalogoClassifica(("placa", Chapa, 1m, ClassificacaoTermo.Encontrado));

        var erro = await Assert.ThrowsAsync<EsclarecimentoNecessarioException>(() => Processar("Quero uma placa"));

        Assert.Equal("Qual o tamanho da placa?", erro.Pergunta);
        Assert.Empty(erro.Sugestoes);
        Assert.Equal("Qual o tamanho da placa?", _conversa.Mensagens.Last().Conteudo);
    }

    [Fact]
    public async Task MaterialId_do_LLM_nao_e_aceito_sem_conferencia()
    {
        // O LLM afirma uma confirmação que nunca foi oferecida: o código refaz a busca e usa o resultado dela.
        LlmResponde(Interpretacao(Item("placa", "m2", quantidade: 1, materialId: "m-qualquer", confirmado: true)));
        CatalogoClassifica(("placa", Chapa, 1m, ClassificacaoTermo.Encontrado));

        await Processar("Placa de 1 m²");

        _ferramentas.Verify(f => f.SalvarProjetoAsync(ProjetoId,
            It.Is<IReadOnlyList<ItemPedido>>(p => p.Single().MaterialId == "m-chapa"), It.IsAny<int?>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Refinamento_mantem_o_material_ja_cotado_sem_nova_busca()
    {
        _projeto.RegistrarCotacao([new ItemProjeto("m-chapa", "Chapa", 0.36m, UnidadeMedida.MetroQuadrado, 120m, 43.20m)], 43.20m, Agora);
        LlmResponde(Interpretacao(Item("placa", "cm", largura: 80, altura: 80, materialId: "m-chapa")));

        await Processar("Troque para 80x80");

        _ferramentas.Verify(f => f.BuscarMateriaisAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _ferramentas.Verify(f => f.SalvarProjetoAsync(ProjetoId,
            It.Is<IReadOnlyList<ItemPedido>>(p => p.Single().Medida == new MedidaInformada(null, "cm", 80, 80)), It.IsAny<int?>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Saida_invalida_uma_vez_e_repetida()
    {
        LlmResponde("isto não é JSON", Interpretacao(Item("placa", "m2", quantidade: 1)));
        CatalogoClassifica(("placa", Chapa, 1m, ClassificacaoTermo.Encontrado));

        await Processar("Placa de 1 m²");

        Assert.Equal(2, _opcoesRecebidas.Count);
    }

    [Fact]
    public async Task Saida_invalida_duas_vezes_e_falha_de_processamento()
    {
        LlmResponde("""{"tipo":"itens","pergunta":null,"itens":[]}""", "nada");

        var erro = await Assert.ThrowsAsync<FalhaProcessamentoException>(() => Processar("Placa"));

        Assert.IsType<InvalidOperationException>(erro.Causa);
        Assert.Equal(FalhaProcessamentoException.MensagemFixa, _conversa.Mensagens.Last().Conteudo);
        _conversas.Verify(c => c.AtualizarAsync(_conversa, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Erro_de_rede_com_o_LLM_e_falha_de_processamento()
    {
        _chat
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("sem rede"));

        var erro = await Assert.ThrowsAsync<FalhaProcessamentoException>(() => Processar("Placa"));

        Assert.IsType<HttpRequestException>(erro.Causa);
    }

    [Fact]
    public async Task Recusa_do_modelo_e_falha_de_processamento()
    {
        _chat
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "")) { FinishReason = ChatFinishReason.ContentFilter });

        await Assert.ThrowsAsync<FalhaProcessamentoException>(() => Processar("Placa"));
    }

    [Fact]
    public async Task Medida_invalida_do_calculo_chega_ao_cliente_como_erro_de_negocio()
    {
        LlmResponde(Interpretacao(Item("placa", "ml", quantidade: 500)));
        CatalogoClassifica(("placa", Chapa, 1m, ClassificacaoTermo.Encontrado));
        _ferramentas
            .Setup(f => f.CalcularAsync(ProjetoId, It.IsAny<IReadOnlyList<ItemPedido>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MedidaInvalidaException("A unidade \"ml\" não pode ser convertida para m2."));

        await Assert.ThrowsAsync<MedidaInvalidaException>(() => Processar("500 ml de placa"));

        Assert.Equal("A unidade \"ml\" não pode ser convertida para m2.", _conversa.Mensagens.Last().Conteudo);
    }

    [Fact]
    public async Task Projeto_arquivado_nao_chama_o_LLM()
    {
        _projeto.RegistrarCotacao([new ItemProjeto("m-chapa", "Chapa", 1m, UnidadeMedida.MetroQuadrado, 120m, 120m)], 120m, Agora);
        typeof(Projeto).GetProperty(nameof(Projeto.Status))!.SetValue(_projeto, StatusProjeto.Arquivado);

        await Assert.ThrowsAsync<ProjetoArquivadoException>(() => Processar("Troque para 80x80"));

        Assert.Empty(_opcoesRecebidas);
    }

    private Task<ResultadoAgente> Processar(string mensagem)
    {
        var relogio = new Mock<TimeProvider>();
        relogio.Setup(r => r.GetUtcNow()).Returns(Agora);
        var agente = new AgenteProjetos(_chat.Object, _ferramentas.Object, _conversas.Object, relogio.Object, NullLogger<AgenteProjetos>.Instance);
        return agente.ProcessarAsync(_projeto, _conversa, mensagem, CancellationToken.None);
    }

    private void LlmResponde(params string[] respostas)
    {
        foreach (var resposta in respostas)
        {
            _respostasDoLlm.Enqueue(resposta);
        }
    }

    private void CatalogoClassifica(params (string Termo, Material Material, decimal Similaridade, ClassificacaoTermo Classificacao)[] resultados)
    {
        _ferramentas
            .Setup(f => f.BuscarMateriaisAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> termos, CancellationToken _) =>
                [.. termos.Select(t => resultados.First(r => r.Termo == t)).Select(r => new ResultadoTermo(r.Termo, r.Material, r.Similaridade, r.Classificacao))]);
    }

    private static string Interpretacao(params object[] itens) =>
        JsonSerializer.Serialize(new { tipo = "itens", pergunta = (string?)null, itens });

    private static object Item(
        string termo,
        string unidade,
        decimal? quantidade = null,
        decimal? largura = null,
        decimal? altura = null,
        string? materialId = null,
        bool confirmado = false) =>
        new { termo, materialId, confirmado, quantidade, unidade, largura, altura };

    private static Material MaterialComId(string id, string nome, string[] sinonimos, UnidadeMedida unidade, decimal preco)
    {
        var material = new Material(nome, sinonimos, TipoMaterial.Material, "Sinalização", unidade, preco, "Fornecedor", Agora);
        typeof(Material).GetProperty(nameof(Material.Id))!.SetValue(material, id);
        return material;
    }

    private static Projeto ProjetoComId()
    {
        var projeto = new Projeto("cliente-1", "Placa de trânsito", Agora);
        typeof(Projeto).GetProperty(nameof(Projeto.Id))!.SetValue(projeto, ProjetoId);
        return projeto;
    }
}
