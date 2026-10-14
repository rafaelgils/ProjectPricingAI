using System.Globalization;
using Microsoft.Extensions.Logging;
using ProjectPricing.Aplicacao.Cotacao;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Agente;

/// <summary>Cotação gravada e a resposta ao cliente.</summary>
public sealed record ResultadoAgente(Projeto Projeto, string Mensagem);

/// <summary>Agente de Projetos; a interface permite simular o agente nos testes dos endpoints.</summary>
public interface IAgenteProjetos
{
    Task<ResultadoAgente> ProcessarAsync(Projeto projeto, Conversa conversa, string mensagemDoCliente, CancellationToken cancellationToken);
}

/// <summary>
/// Agente de Projetos (architecture.md §5.3 a §5.5): orquestra as etapas. Interpretar a descrição é do
/// <see cref="IInterpretadorDeDescricao"/> e casar termos com o catálogo é do <see cref="IResolvedorDeMateriais"/>.
/// A ordem de decisão, o cálculo e a gravação são do código, nunca do LLM (ADR-001, ADR-006).
/// </summary>
public sealed partial class AgenteProjetos(
    IInterpretadorDeDescricao interpretador,
    IResolvedorDeMateriais resolvedor,
    IFerramentasCotacao ferramentas,
    IConversaRepository conversas,
    TimeProvider relogio,
    ILogger<AgenteProjetos> logger) : IAgenteProjetos
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

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
        var interpretacao = await interpretador.InterpretarAsync(projeto, conversa, cancellationToken);
        var resolucao = await resolvedor.ResolverAsync(projeto, conversa, interpretacao.Itens, cancellationToken);

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
        var salvo = await ferramentas.SalvarProjetoAsync(projeto.Id!, resolucao.Pedidos, projeto.Versao, cancellationToken);

        var mensagem = $"Para esse projeto o valor estimado é {calculo.Total.ToString("C", PtBr)}.";
        return new ResultadoAgente(salvo, mensagem);
    }

    private void RegistrarSugestoesPendentes(Conversa conversa, IReadOnlyList<SugestaoMaterial> sugestoes) =>
        conversa.AdicionarMensagem(new Mensagem(PapelMensagem.Tool, RegistroDeSugestoes.Serializar(sugestoes), relogio.GetUtcNow()));

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha do agente ao processar o projeto {ProjetoId}.")]
    private static partial void LogFalha(ILogger logger, string? projetoId, Exception erro);
}

/// <summary>Nomes das tools no Servidor MCP e no LLM (ADR-006).</summary>
public static class NomesFerramentas
{
    public const string BuscarMateriais = "buscarMateriais";
    public const string Calcular = "calcular";
    public const string SalvarProjeto = "salvarProjeto";
}
