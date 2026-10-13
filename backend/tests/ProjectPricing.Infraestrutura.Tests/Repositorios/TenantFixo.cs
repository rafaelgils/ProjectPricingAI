using ProjectPricing.Dominio.Tenancy;

namespace ProjectPricing.Infraestrutura.Tests.Repositorios;

/// <summary>Tenant fixo para os testes dos repositórios.</summary>
public sealed class TenantFixo(string tenantId = TenantFixo.Padrao) : ITenantAtual
{
    public const string Padrao = "empresa-a";

    public string TenantId { get; } = tenantId;
}
