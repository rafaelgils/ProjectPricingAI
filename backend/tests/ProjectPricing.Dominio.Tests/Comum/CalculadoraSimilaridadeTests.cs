using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Dominio.Tests.Comum;

/// <summary>RN09: Levenshtein normalizado, com os exemplos citados na documentação.</summary>
public class CalculadoraSimilaridadeTests
{
    [Theory]
    [InlineData("película reflexiva", "Película refletiva", 0.94)]
    [InlineData("tinta reflexiva", "Película refletiva", 0.61)]
    [InlineData("placa", "Chapa de aço galvanizado", 0.17)]
    [InlineData("  CABEÇOTE de Metal ", "cabecote de metal", 1.00)]
    [InlineData("", "", 1.00)]
    public void Similaridade_dos_exemplos_da_documentacao(string a, string b, double esperado)
    {
        Assert.Equal((decimal)esperado, Math.Round(CalculadoraSimilaridade.Calcular(a, b), 2));
    }

    [Fact]
    public void E_simetrica()
    {
        Assert.Equal(
            CalculadoraSimilaridade.Calcular("cantoneira", "Suporte em L"),
            CalculadoraSimilaridade.Calcular("Suporte em L", "cantoneira"));
    }
}

public class ClassificadorTermosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("abcdefghij", ClassificacaoTermo.Encontrado)] // 100%
    [InlineData("abcdefghxy", ClassificacaoTermo.AConfirmar)] // 80%: limite inferior da confirmação
    [InlineData("abcdefgxyz", ClassificacaoTermo.SemCorrespondencia)] // 70%
    public void Limites_de_100_e_80_por_cento(string termo, ClassificacaoTermo esperada)
    {
        var resultado = ClassificadorTermos.Classificar(termo, [Material("abcdefghij")]);

        Assert.Equal(esperada, resultado.Classificacao);
    }

    [Fact]
    public void Sinonimo_conta_como_nome_e_vale_o_material_mais_parecido()
    {
        var chapa = Material("Chapa de aço galvanizado", "placa");
        var pelicula = Material("Película refletiva", "tinta reflexiva");

        var resultado = ClassificadorTermos.Classificar("Placa", [pelicula, chapa]);

        Assert.Same(chapa, resultado.Material);
        Assert.Equal(1.00m, resultado.Similaridade);
        Assert.Equal(ClassificacaoTermo.Encontrado, resultado.Classificacao);
        Assert.Equal("Placa", resultado.Termo);
    }

    [Fact]
    public void Catalogo_vazio_nao_tem_correspondencia()
    {
        var resultado = ClassificadorTermos.Classificar("placa", []);

        Assert.Null(resultado.Material);
        Assert.Equal(ClassificacaoTermo.SemCorrespondencia, resultado.Classificacao);
    }

    private static Material Material(string nome, params string[] sinonimos) =>
        new(nome, sinonimos, TipoMaterial.Material, "Sinalização", UnidadeMedida.MetroQuadrado, 1m, "F", Agora);
}

public class ProjetoCotacaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);
    private static readonly ItemProjeto Chapa = new("m-chapa", "Chapa", 0.36m, UnidadeMedida.MetroQuadrado, 120m, 43.20m);

    [Fact]
    public void Registrar_cotacao_passa_o_projeto_para_cotado()
    {
        var projeto = new Projeto("cliente", "Placa", Agora.AddHours(-1));

        projeto.RegistrarCotacao([Chapa], 43.20m, Agora);

        Assert.Equal(StatusProjeto.Cotado, projeto.Status);
        Assert.Equal(43.20m, projeto.ValorTotal);
        Assert.Equal(Agora, projeto.AlteradoEm);
        Assert.Same(Chapa, projeto.ItemDoMaterial("m-chapa"));
        Assert.Null(projeto.ItemDoMaterial("outro"));
    }

    [Fact]
    public void Cotacao_precisa_de_itens()
    {
        Assert.Throws<ArgumentException>(() => new Projeto("cliente", "Placa", Agora).RegistrarCotacao([], 0m, Agora));
    }

    [Fact]
    public void Projeto_arquivado_nao_recebe_cotacao()
    {
        var projeto = new Projeto("cliente", "Placa", Agora);
        typeof(Projeto).GetProperty(nameof(Projeto.Status))!.SetValue(projeto, StatusProjeto.Arquivado);

        Assert.Throws<ProjetoArquivadoException>(() => projeto.RegistrarCotacao([Chapa], 43.20m, Agora));
    }
}
