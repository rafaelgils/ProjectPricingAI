using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ProjectPricing.Aplicacao.Cotacao;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Precificacao;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Agente;

/// <summary>Cotação gravada e a resposta ao cliente.</summary>
public sealed record ResultadoAgente(Projeto Projeto, string Mensagem);

/// <summary>
/// Agente de Projetos (architecture.md §5.3 a §5.5). O LLM só interpreta a descrição e chama buscarMateriais;
/// a classificação da RN09, a ordem de decisão, o cálculo e a gravação são do código (ADR-001, ADR-006).
/// </summary>
public sealed partial class AgenteProjetos(
    IChatClient chat,
    IFerramentasCotacao ferramentas,
    IConversaRepository conversas,
    TimeProvider relogio,
    ILogger<AgenteProjetos> logger)
{
    /// <summary>Uma nova tentativa quando a saída do LLM não segue o schema (plano, F6).</summary>
    private const int TentativasDeInterpretacao = 2;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly JsonSerializerOptions OpcoesJson = CriarOpcoesJson();

    /// <summary>
    /// Processa a descrição inicial ou uma mensagem de refinamento. Grava a cotação quando todos os itens estão
    /// resolvidos; caso contrário lança a exceção de domínio que vira o erro da resposta (RN03, RN06, RN09, RN12).
    /// As mensagens da rodada ficam na conversa em qualquer caso.
    /// </summary>
    public async Task<ResultadoAgente> ProcessarAsync(
        Projeto projeto,
        Conversa conversa,
        string mensagemDoCliente,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projeto);
        ArgumentNullException.ThrowIfNull(conversa);
        ArgumentException.ThrowIfNullOrWhiteSpace(mensagemDoCliente);

        projeto.GarantirQueNaoEstaArquivado();
        conversa.AdicionarMensagem(new Mensagem(PapelMensagem.Usuario, mensagemDoCliente, relogio.GetUtcNow()));
        try
        {
            var resultado = await CotarAsync(projeto, conversa, cancellationToken);
            Responder(conversa, resultado.Mensagem);
            return resultado;
        }
        catch (EsclarecimentoNecessarioException esclarecimento)
        {
            RegistrarSugestoesPendentes(conversa, esclarecimento.Sugestoes);
            Responder(conversa, esclarecimento.Pergunta);
            throw;
        }
        catch (ExcecaoDeDominio erro)
        {
            Responder(conversa, DescreverErro(erro));
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception erro)
        {
            // RN12: qualquer falha do LLM ou do agente vira a mensagem fixa; o detalhe fica só no log.
            LogFalha(logger, projeto.Id, erro);
            Responder(conversa, FalhaProcessamentoException.MensagemFixa);
            throw new FalhaProcessamentoException(erro);
        }
        finally
        {
            await conversas.AtualizarAsync(conversa, CancellationToken.None);
        }
    }

    private async Task<ResultadoAgente> CotarAsync(Projeto projeto, Conversa conversa, CancellationToken cancellationToken)
    {
        var interpretacao = await InterpretarAsync(projeto, conversa, cancellationToken);
        var resolucao = await ResolverMateriaisAsync(projeto, conversa, interpretacao.Itens, cancellationToken);

        // Ordem de decisão (plano, F6): não encontrado, depois a confirmar, depois medida faltando.
        if (resolucao.NaoEncontrados.Count > 0)
        {
            throw new ItensNaoEncontradosException(projeto.Id!, resolucao.NaoEncontrados);
        }

        if (resolucao.Sugestoes.Count > 0)
        {
            throw new EsclarecimentoNecessarioException(projeto.Id!, PerguntarConfirmacao(resolucao.Sugestoes), resolucao.Sugestoes);
        }

        if (interpretacao.Tipo == TipoInterpretacao.Esclarecimento)
        {
            throw new EsclarecimentoNecessarioException(projeto.Id!, interpretacao.Pergunta!, []);
        }

        // O agente chama calcular e salvarProjeto pelo código; o LLM não tem acesso a elas (ADR-006).
        var calculo = await ferramentas.CalcularAsync(projeto.Id!, resolucao.Pedidos, cancellationToken);
        var salvo = await ferramentas.SalvarProjetoAsync(projeto.Id!, resolucao.Pedidos, cancellationToken);

        var mensagem = $"Para esse projeto o valor estimado é {calculo.Total.ToString("C", PtBr)}.";
        return new ResultadoAgente(salvo, mensagem);
    }

    private async Task<InterpretacaoLlm> InterpretarAsync(Projeto projeto, Conversa conversa, CancellationToken cancellationToken)
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
            var resposta = await chat.GetResponseAsync<InterpretacaoLlm>(mensagens, OpcoesJson, opcoes, cancellationToken: cancellationToken);
            GarantirQueNaoFoiRecusado(resposta);
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

    private async Task<ResolucaoDeMateriais> ResolverMateriaisAsync(
        Projeto projeto,
        Conversa conversa,
        IReadOnlyList<ItemInterpretado> itens,
        CancellationToken cancellationToken)
    {
        var pendentes = LerSugestoesPendentes(conversa).Select(s => s.MaterialId).ToHashSet(StringComparer.Ordinal);
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
                $"- materialId={material.Id}; nome={material.Nome}; categoria={material.Categoria}; unidade={CodigosEnum<UnidadeMedida>.Codigo(material.Unidade)}");
        }

        pedido.AppendLine().AppendLine("Termos sem correspondência:");
        termos.ForEach(t => pedido.AppendLine(CultureInfo.InvariantCulture, $"- {t}"));

        List<ChatMessage> mensagens = [new(ChatRole.System, PromptsAgente.Sugestao), new(ChatRole.User, pedido.ToString())];
        var resposta = await chat.GetResponseAsync<SugestoesLlm>(mensagens, OpcoesJson, cancellationToken: cancellationToken);
        GarantirQueNaoFoiRecusado(resposta);
        var sugeridos = resposta.TryGetResult(out var saida) ? saida.Sugestoes : [];

        foreach (var termo in termos)
        {
            var id = sugeridos.FirstOrDefault(s => s.Termo == termo)?.MaterialId;
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
                SerializerOptions = OpcoesJson,
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
                    $"- materialId={item.MaterialId}; nome={item.NomeSnapshot}; quantidade={item.Quantidade}; unidade={CodigosEnum<UnidadeMedida>.Codigo(item.Unidade)}");
            }
        }

        var pendentes = LerSugestoesPendentes(conversa);
        if (pendentes.Count > 0)
        {
            contexto.AppendLine().AppendLine().AppendLine("## Sugestões aguardando confirmação do cliente");
            foreach (var sugestao in pendentes)
            {
                contexto.AppendLine(CultureInfo.InvariantCulture,
                    $"- termo \"{sugestao.Termo}\" → materialId={sugestao.MaterialId}; nome={sugestao.Nome}");
            }
        }

        return contexto.ToString();
    }

    /// <summary>Sugestões oferecidas na última pergunta de confirmação, registradas na conversa como mensagem tool.</summary>
    private static IReadOnlyList<SugestaoMaterial> LerSugestoesPendentes(Conversa conversa)
    {
        var registro = conversa.Mensagens.LastOrDefault(m => m.Papel == PapelMensagem.Tool);
        return registro is null
            ? []
            : JsonSerializer.Deserialize<RegistroSugestoes>(registro.Conteudo, OpcoesJson)?.Sugestoes ?? [];
    }

    private void RegistrarSugestoesPendentes(Conversa conversa, IReadOnlyList<SugestaoMaterial> sugestoes)
    {
        var registro = JsonSerializer.Serialize(new RegistroSugestoes(sugestoes), OpcoesJson);
        conversa.AdicionarMensagem(new Mensagem(PapelMensagem.Tool, registro, relogio.GetUtcNow()));
    }

    private void Responder(Conversa conversa, string texto) =>
        conversa.AdicionarMensagem(new Mensagem(PapelMensagem.Assistente, texto, relogio.GetUtcNow()));

    private static string PerguntarConfirmacao(IReadOnlyList<SugestaoMaterial> sugestoes)
    {
        var perguntas = sugestoes.Select(s => s.Origem == OrigemSugestao.Agente
            ? $"\"{s.Termo}\" pode ser \"{s.Nome}\" (sugerido pelo assistente)?"
            : $"você quis dizer \"{s.Nome}\" para \"{s.Termo}\"?");
        return "Antes de calcular, confirme: " + string.Join(" ", perguntas);
    }

    private static string DescreverErro(ExcecaoDeDominio erro) => erro switch
    {
        ItensNaoEncontradosException naoEncontrados =>
            "Não encontrei no catálogo: " + string.Join(", ", naoEncontrados.ItensNaoEncontrados) +
            ". Ajuste a descrição ou peça ao administrador o cadastro desses itens.",
        _ => erro.Message,
    };

    private static ItemPedido ParaPedido(ItemInterpretado item, string materialId) =>
        new(materialId, new MedidaInformada(item.Quantidade, item.Unidade, item.Largura, item.Altura));

    private static bool EhValida(InterpretacaoLlm interpretacao) => interpretacao.Tipo switch
    {
        TipoInterpretacao.Esclarecimento => !string.IsNullOrWhiteSpace(interpretacao.Pergunta),
        _ => interpretacao.Itens is { Count: > 0 }
             && interpretacao.Itens.All(i => !string.IsNullOrWhiteSpace(i.Termo) && !string.IsNullOrWhiteSpace(i.Unidade)),
    };

    /// <summary>Recusa dos classificadores de segurança (stop_reason refusal) é falha de processamento (RN12).</summary>
    private static void GarantirQueNaoFoiRecusado(ChatResponse resposta)
    {
        if (resposta.FinishReason == ChatFinishReason.ContentFilter)
        {
            throw new InvalidOperationException("O LLM recusou a solicitação.");
        }
    }

    private static JsonSerializerOptions CriarOpcoesJson()
    {
        var opcoes = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions);
        opcoes.Converters.Add(new JsonStringEnumConverter());
        opcoes.MakeReadOnly();
        return opcoes;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha do agente ao processar o projeto {ProjetoId}.")]
    private static partial void LogFalha(ILogger logger, string? projetoId, Exception erro);

    private sealed record RegistroSugestoes(IReadOnlyList<SugestaoMaterial> Sugestoes);

    private sealed class ResolucaoDeMateriais
    {
        public List<ItemPedido> Pedidos { get; } = [];

        public List<SugestaoMaterial> Sugestoes { get; } = [];

        public List<string> NaoEncontrados { get; } = [];
    }
}

/// <summary>Nomes das tools no Servidor MCP e no LLM (ADR-006).</summary>
public static class NomesFerramentas
{
    public const string BuscarMateriais = "buscarMateriais";
    public const string Calcular = "calcular";
    public const string SalvarProjeto = "salvarProjeto";
}
