namespace ProjectPricing.Dominio.Tenancy;

/// <summary>Base das entidades que pertencem a uma empresa: guarda o tenant uma vez, sem repetir o código (DRY).</summary>
public abstract class EntidadeDoTenant : IPertenceAoTenant
{
    /// <summary>Empresa dona do registro; definida pelo repositório na inserção (ADR-011).</summary>
    public string TenantId { get; private set; } = string.Empty;

    public void DefinirTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        TenantId = tenantId;
    }
}
