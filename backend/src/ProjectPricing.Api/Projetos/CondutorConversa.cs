using Microsoft.Extensions.Options;
using ProjectPricing.Api.Erros;
using ProjectPricing.Api.Sse;
using ProjectPricing.Aplicacao.Agente;
using ProjectPricing.Aplicacao.Projetos;
using ProjectPricing.Dominio.Excecoes;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace ProjectPricing.Api.Projetos;

/// <summary>
/// Uma rodada da conversa em SSE: aviso imediato (RNF02), processamento pelo agente e o resultado como
/// evento cotacao ou erro, sempre terminando com fim (standards.md §5).
/// </summary>
public sealed partial class CondutorConversa(
    IAgenteProjetos agente,
    IOptions<JsonOptions> opcoesJson,
    ILogger<CondutorConversa> logger)
{
    public const string AvisoDescricao = "Analisando a descrição do projeto...";
    public const string AvisoMensagem = "Analisando a sua mensagem...";

    public async Task ResponderAsync(
        HttpResponse resposta,
        int status,
        string? location,
        ProjetoEmConversa rodada,
        string mensagemDoCliente,
        string aviso,
        CancellationToken cancellationToken)
    {
        var sse = new EscritorSse(resposta, opcoesJson.Value.SerializerOptions);
        await sse.IniciarAsync(status, location, cancellationToken);
        await sse.EnviarAsync(EventosSse.Delta, new DeltaResposta(aviso), cancellationToken);

        var projeto = rodada.Projeto;
        try
        {
            var resultado = await agente.ProcessarAsync(projeto, rodada.Conversa, mensagemDoCliente, cancellationToken);
            projeto = resultado.Projeto;
            await sse.EnviarAsync(EventosSse.Delta, new DeltaResposta(resultado.Mensagem), cancellationToken);
            await sse.EnviarAsync(EventosSse.Cotacao, ProjetoResposta.De(projeto), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // O cliente fechou a conexão: não há para quem enviar o resto.
            return;
        }
        catch (Exception erro)
        {
            if (erro is not ExcecaoDeDominio)
            {
                LogErroInesperado(logger, projeto.Id, erro);
            }

            // Os headers já foram enviados: o erro vai como evento, em Problem Details (RN03, RN06, RN09, RN12).
            await sse.EnviarAsync(EventosSse.Erro, MapeadorDeErros.Mapear(erro), cancellationToken);
        }

        await sse.EnviarAsync(EventosSse.Fim, new FimResposta(projeto.Id!, projeto.Status), cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro inesperado na conversa do projeto {ProjetoId}.")]
    private static partial void LogErroInesperado(ILogger logger, string? projetoId, Exception erro);
}
