namespace ProjectPricing.Dominio.Excecoes;

/// <summary>O usuário autenticado não pertence a nenhuma empresa (token sem a claim de tenant).</summary>
public sealed class TenantNaoIdentificadoException : ExcecaoDeDominio
{
    public const string CodigoErro = "TENANT_NAO_IDENTIFICADO";

    public TenantNaoIdentificadoException()
        : base(CodigoErro, "O usuário não está associado a nenhuma empresa.")
    {
    }
}
