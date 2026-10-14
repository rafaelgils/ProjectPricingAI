namespace ProjectPricing.Dominio.Tenancy;

/// <summary>
/// Empresa (tenant) da requisição. O valor vem da claim <c>tenant_id</c> do JWT e nunca de parâmetro do cliente,
/// para que um usuário não consiga escolher o tenant que consulta (ADR-011).
/// </summary>
public interface ITenantAtual
{
    /// <exception cref="Excecoes.TenantNaoIdentificadoException">O token não traz o tenant.</exception>
    string TenantId { get; }
}

/// <summary>Entidade que pertence a um tenant; o repositório grava o tenant na inserção.</summary>
public interface IPertenceAoTenant
{
    string TenantId { get; }

    void DefinirTenant(string tenantId);
}
