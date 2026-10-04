namespace ProjectPricing.Dominio.Excecoes;

/// <summary>
/// A medida informada não pode ser convertida para a unidade do material (RN05): unidade incompatível,
/// medida faltando ou quantidade que não é positiva.
/// </summary>
public sealed class MedidaInvalidaException : ExcecaoDeDominio
{
    public const string CodigoErro = "MEDIDA_INVALIDA";

    public MedidaInvalidaException(string mensagem)
        : base(CodigoErro, mensagem)
    {
    }
}
