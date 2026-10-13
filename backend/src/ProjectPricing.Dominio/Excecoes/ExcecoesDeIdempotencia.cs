namespace ProjectPricing.Dominio.Excecoes;

/// <summary>A mesma <c>Idempotency-Key</c> foi enviada com um conteúdo diferente do original.</summary>
public sealed class ChaveIdempotenciaReutilizadaException : ExcecaoDeDominio
{
    public const string CodigoErro = "IDEMPOTENCIA_CONFLITO";

    public ChaveIdempotenciaReutilizadaException()
        : base(CodigoErro, "Esta Idempotency-Key já foi usada em outra requisição com conteúdo diferente.")
    {
    }
}

/// <summary>Outra chamada com a mesma <c>Idempotency-Key</c> ainda está em processamento.</summary>
public sealed class RequisicaoEmAndamentoException : ExcecaoDeDominio
{
    public const string CodigoErro = "REQUISICAO_EM_ANDAMENTO";

    public RequisicaoEmAndamentoException()
        : base(CodigoErro, "Uma requisição com esta Idempotency-Key ainda está sendo processada.")
    {
    }
}
