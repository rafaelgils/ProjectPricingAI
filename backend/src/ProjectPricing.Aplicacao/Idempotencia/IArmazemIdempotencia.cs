namespace ProjectPricing.Aplicacao.Idempotencia;

/// <summary>Resposta guardada de uma requisição já concluída, para ser reenviada sem reprocessar (ADR-012).</summary>
/// <param name="Corpo">Resposta SSE completa, exatamente como foi enviada.</param>
public sealed record RespostaGuardada(int StatusHttp, string? Location, string Corpo);

public enum SituacaoReserva
{
    /// <summary>Primeira vez que a chave aparece: a requisição deve ser processada.</summary>
    Nova,

    /// <summary>A mesma requisição já foi concluída: reenviar <see cref="ResultadoReserva.Resposta"/>.</summary>
    Repetida,

    /// <summary>A mesma requisição ainda está sendo processada por outra chamada.</summary>
    EmAndamento,

    /// <summary>A chave já foi usada com outro conteúdo.</summary>
    Conflito,
}

public sealed record ResultadoReserva(SituacaoReserva Situacao, RespostaGuardada? Resposta = null);

/// <summary>
/// Guarda o resultado das requisições que carregam <c>Idempotency-Key</c>. A chave vale por tenant e usuário:
/// duas empresas ou dois usuários podem usar a mesma chave sem interferir.
/// </summary>
public interface IArmazemIdempotencia
{
    /// <param name="chave">Valor do cabeçalho <c>Idempotency-Key</c>.</param>
    /// <param name="impressaoDigital">Resumo do conteúdo da requisição; detecta a reutilização da chave com outro pedido.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    Task<ResultadoReserva> ReservarAsync(string chave, string impressaoDigital, CancellationToken cancellationToken);

    Task ConcluirAsync(string chave, RespostaGuardada resposta, CancellationToken cancellationToken);

    /// <summary>Libera a chave sem guardar resultado (falha ou desistência): uma nova tentativa é processada de novo.</summary>
    Task LiberarAsync(string chave, CancellationToken cancellationToken);
}
