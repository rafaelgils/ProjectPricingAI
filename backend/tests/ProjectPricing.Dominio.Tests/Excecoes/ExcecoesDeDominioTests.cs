using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Dominio.Tests.Excecoes;

public class ExcecoesDeDominioTests
{
    [Fact]
    public void Projeto_arquivado_usa_a_mensagem_da_RN11()
    {
        var erro = new ProjetoArquivadoException();

        Assert.Equal("PROJETO_ARQUIVADO", erro.Codigo);
        Assert.Equal("Este projeto está inativo.", erro.Message);
    }

    [Fact]
    public void Falha_de_processamento_usa_a_mensagem_fixa_da_RN12_e_guarda_a_causa()
    {
        var causa = new TimeoutException("LLM demorou");
        var erro = new FalhaProcessamentoException(causa);

        Assert.Equal("FALHA_PROCESSAMENTO", erro.Codigo);
        Assert.Equal(
            "Não foi possível processar essa mensagem no momento, favor contate o administrador",
            erro.Message);
        Assert.Same(causa, erro.Causa);
    }

    [Fact]
    public void Itens_nao_encontrados_listam_os_termos()
    {
        var erro = new ItensNaoEncontradosException("projeto-1", ["cabeçote de metal"]);

        Assert.Equal("ITENS_NAO_ENCONTRADOS", erro.Codigo);
        Assert.Equal("projeto-1", erro.ProjetoId);
        Assert.Equal(["cabeçote de metal"], erro.ItensNaoEncontrados);
    }

    [Fact]
    public void Esclarecimento_traz_pergunta_e_sugestoes()
    {
        SugestaoMaterial[] sugestoes =
            [new("película reflexiva", "id-1", "Película refletiva", OrigemSugestao.Similaridade, 0.94m)];

        var erro = new EsclarecimentoNecessarioException("projeto-1", "Você quis dizer Película refletiva?", sugestoes);

        Assert.Equal("ESCLARECIMENTO_NECESSARIO", erro.Codigo);
        Assert.Equal("projeto-1", erro.ProjetoId);
        Assert.Equal("Você quis dizer Película refletiva?", erro.Pergunta);
        Assert.Equal(sugestoes, erro.Sugestoes);
    }

    [Fact]
    public void Material_duplicado_e_recurso_nao_encontrado_identificam_o_problema()
    {
        var duplicado = new MaterialDuplicadoException("placa");
        var naoEncontrado = new RecursoNaoEncontradoException("Projeto", "abc");

        Assert.Equal("MATERIAL_DUPLICADO", duplicado.Codigo);
        Assert.Equal("placa", duplicado.TermoDuplicado);
        Assert.Contains("placa", duplicado.Message, StringComparison.Ordinal);
        Assert.Equal("RECURSO_NAO_ENCONTRADO", naoEncontrado.Codigo);
        Assert.Equal("Projeto \"abc\" não encontrado.", naoEncontrado.Message);
    }
}
