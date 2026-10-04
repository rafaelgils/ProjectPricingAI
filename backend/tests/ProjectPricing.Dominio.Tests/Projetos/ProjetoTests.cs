using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Dominio.Tests.Projetos;

public class ProjetoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 14, 30, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Novo_projeto_comeca_em_rascunho_sem_itens_e_sem_valor()
    {
        var projeto = new Projeto("keycloak-123", "  Placa de trânsito 60x60 ", Agora);

        Assert.Null(projeto.Id);
        Assert.Equal("keycloak-123", projeto.ClienteId);
        Assert.Equal("Placa de trânsito 60x60", projeto.Descricao);
        Assert.Equal(StatusProjeto.Rascunho, projeto.Status);
        Assert.Empty(projeto.Itens);
        Assert.Null(projeto.ValorTotal);
        Assert.Equal(Agora, projeto.CriadoEm);
        Assert.Equal(Agora, projeto.AlteradoEm);
        Assert.Equal(0, projeto.Versao);
    }

    [Theory]
    [InlineData("", "Descrição")]
    [InlineData("cliente", " ")]
    public void Cliente_e_descricao_obrigatorios(string clienteId, string descricao)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Projeto(clienteId, descricao, Agora));
    }

    [Fact]
    public void Item_guarda_os_valores_congelados()
    {
        var item = new ItemProjeto("6703e0aa0000000000000001", "Película refletiva", 0.36m, UnidadeMedida.MetroQuadrado, 95.00m, 34.20m);

        Assert.Equal("6703e0aa0000000000000001", item.MaterialId);
        Assert.Equal("Película refletiva", item.NomeSnapshot);
        Assert.Equal(0.36m, item.Quantidade);
        Assert.Equal(UnidadeMedida.MetroQuadrado, item.Unidade);
        Assert.Equal(95.00m, item.PrecoUnitarioSnapshot);
        Assert.Equal(34.20m, item.Subtotal);
    }

    [Theory]
    [InlineData("", "Nome", 1, 1, 1)]
    [InlineData("id", "", 1, 1, 1)]
    [InlineData("id", "Nome", 0, 1, 1)]
    [InlineData("id", "Nome", 1, 0, 1)]
    [InlineData("id", "Nome", 1, 1, -0.01)]
    public void Item_rejeita_valores_invalidos(string materialId, string nome, decimal quantidade, decimal preco, decimal subtotal)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new ItemProjeto(materialId, nome, quantidade, UnidadeMedida.Unidade, preco, subtotal));
    }

    [Fact]
    public void Sugestao_de_material_guarda_a_origem()
    {
        var sugestao = new SugestaoMaterial("cantoneira", "id-7", "Suporte em L de aço", OrigemSugestao.Agente, null);

        Assert.Equal(OrigemSugestao.Agente, sugestao.Origem);
        Assert.Null(sugestao.Similaridade);
    }
}
