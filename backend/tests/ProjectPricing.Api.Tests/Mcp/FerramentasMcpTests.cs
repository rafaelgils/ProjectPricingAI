using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using Moq;
using ProjectPricing.Api.Mcp;
using ProjectPricing.Aplicacao.Cotacao;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Precificacao;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Api.Tests.Mcp;

/// <summary>O Servidor MCP só traduz o protocolo; as regras são de IFerramentasCotacao (ADR-006).</summary>
public class FerramentasMcpTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);
    private readonly Mock<IFerramentasCotacao> _ferramentas = new();

    [Fact]
    public void Expoe_as_tres_tools_do_ADR_006_com_descricao()
    {
        var tools = typeof(FerramentasMcp).GetMethods()
            .Select(m => (Metodo: m, Atributo: m.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(t => t.Atributo is not null)
            .ToList();

        Assert.Equal(["buscarMateriais", "calcular", "salvarProjeto"], tools.Select(t => t.Atributo!.Name).Order());
        Assert.All(tools, t => Assert.NotNull(t.Metodo.GetCustomAttribute<DescriptionAttribute>()));
        Assert.NotNull(typeof(FerramentasMcp).GetCustomAttribute<McpServerToolTypeAttribute>());
    }

    [Fact]
    public async Task BuscarMateriais_devolve_classificacao_e_material_mais_proximo()
    {
        var chapa = new Material("Chapa de aço galvanizado", ["placa"], TipoMaterial.Material, "Sinalização", UnidadeMedida.MetroQuadrado, 120m, "F", Agora);
        typeof(Material).GetProperty(nameof(Material.Id))!.SetValue(chapa, "m-chapa");
        _ferramentas
            .Setup(f => f.BuscarMateriaisAsync(It.Is<IReadOnlyList<string>>(t => t.Single() == "placa"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ResultadoTermo("placa", chapa, 1m, ClassificacaoTermo.Encontrado)]);

        var resultado = await new FerramentasMcp(_ferramentas.Object).BuscarMateriais(["placa"], CancellationToken.None);

        Assert.Equal(new ResultadoBuscaDto("placa", ClassificacaoTermo.Encontrado, 1m, "m-chapa", "Chapa de aço galvanizado", "m2"), resultado.Single());
    }

    [Fact]
    public async Task Calcular_converte_os_itens_e_devolve_valores_com_moeda()
    {
        _ferramentas
            .Setup(f => f.CalcularAsync("p1", It.Is<IReadOnlyList<ItemPedido>>(i => i.Single().Medida == new MedidaInformada(null, "cm", 60, 60)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoCalculo([new ItemCalculado("m-chapa", "Chapa", 0.36m, UnidadeMedida.MetroQuadrado, 120m, 43.20m)], 43.20m));

        var calculo = await new FerramentasMcp(_ferramentas.Object)
            .Calcular("p1", [new ItemPedidoDto("m-chapa", null, "cm", 60, 60)], CancellationToken.None);

        Assert.Equal(43.20m, calculo.ValorTotal);
        Assert.Equal("BRL", calculo.Moeda);
        Assert.Equal("m2", calculo.Itens.Single().Unidade);
    }

    [Fact]
    public async Task Erro_de_negocio_chega_ao_cliente_MCP_com_code_e_mensagem()
    {
        _ferramentas
            .Setup(f => f.CalcularAsync("p1", It.IsAny<IReadOnlyList<ItemPedido>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Dominio.Excecoes.MedidaInvalidaException("A unidade \"ml\" não pode ser convertida para m2."));

        var erro = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() =>
            new FerramentasMcp(_ferramentas.Object).Calcular("p1", [], CancellationToken.None));

        Assert.Equal("MEDIDA_INVALIDA: A unidade \"ml\" não pode ser convertida para m2.", erro.Message);
    }

    [Fact]
    public async Task SalvarProjeto_devolve_a_cotacao_gravada()
    {
        var projeto = new Projeto("cliente", "Placa", Agora);
        projeto.RegistrarCotacao([new ItemProjeto("m-chapa", "Chapa", 0.36m, UnidadeMedida.MetroQuadrado, 120m, 43.20m)], 43.20m, Agora);
        _ferramentas
            .Setup(f => f.SalvarProjetoAsync("p1", It.IsAny<IReadOnlyList<ItemPedido>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projeto);

        var calculo = await new FerramentasMcp(_ferramentas.Object)
            .SalvarProjeto("p1", [new ItemPedidoDto("m-chapa", null, "cm", 60, 60)], CancellationToken.None);

        Assert.Equal(43.20m, calculo.ValorTotal);
        Assert.Equal(120m, calculo.Itens.Single().PrecoUnitario);
    }
}
