using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;

namespace ProjectPricing.Dominio.Tests.Comum;

public class NormalizadorTextoTests
{
    [Theory]
    [InlineData("Película Refletiva", "pelicula refletiva")]
    [InlineData("  CABEÇOTE   de  metal ", "cabecote de metal")]
    [InlineData("Instalação\telétrica", "instalacao eletrica")]
    [InlineData("", "")]
    public void Remove_acentos_maiusculas_e_espacos_extras(string entrada, string esperado)
    {
        Assert.Equal(esperado, NormalizadorTexto.Normalizar(entrada));
    }

    [Fact]
    public void Textos_equivalentes_ignoram_acento_e_maiuscula()
    {
        Assert.True(NormalizadorTexto.SaoEquivalentes("Chapa de Aço", "chapa de aco"));
        Assert.False(NormalizadorTexto.SaoEquivalentes("Chapa", "Chapas"));
    }

    [Fact]
    public void Texto_nulo_e_erro()
    {
        Assert.Throws<ArgumentNullException>(() => NormalizadorTexto.Normalizar(null!));
    }
}

public class CodigosEnumTests
{
    [Theory]
    [InlineData(UnidadeMedida.MetroQuadrado, "m2")]
    [InlineData(UnidadeMedida.Litro, "L")]
    public void Codigo_e_o_da_documentacao(UnidadeMedida unidade, string codigo)
    {
        Assert.Equal(codigo, CodigosEnum<UnidadeMedida>.Codigo(unidade));
        Assert.True(CodigosEnum<UnidadeMedida>.TentarConverter(codigo, out var lido));
        Assert.Equal(unidade, lido);
    }

    [Theory]
    [InlineData("Inativo")]
    [InlineData("desativado")]
    [InlineData(null)]
    public void Codigo_desconhecido_ou_com_outra_caixa_nao_converte(string? codigo)
    {
        Assert.False(CodigosEnum<StatusMaterial>.TentarConverter(codigo, out _));
    }

    [Fact]
    public void Lista_todos_os_codigos()
    {
        Assert.Equal(["ativo", "inativo"], CodigosEnum<StatusMaterial>.Codigos.Order());
    }
}
