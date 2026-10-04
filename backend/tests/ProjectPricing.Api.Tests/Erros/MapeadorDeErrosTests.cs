using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectPricing.Api.Erros;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Api.Tests.Erros;

/// <summary>Formato de erro de standards.md §5 e exemplos de business-rules.md §8.</summary>
public class MapeadorDeErrosTests
{
    [Fact]
    public void Itens_nao_encontrados_viram_422_com_a_lista_e_o_projeto()
    {
        var problema = MapeadorDeErros.Mapear(new ItensNaoEncontradosException("6703f1c2a9", ["cabeçote de metal"]));

        Assert.Equal(422, problema.Status);
        Assert.Equal("https://precificacao/erros/itens-nao-encontrados", problema.Type);
        Assert.Equal("Itens não encontrados no catálogo", problema.Title);
        Assert.Equal("ITENS_NAO_ENCONTRADOS", problema.Extensions["code"]);
        Assert.Equal(new[] { "cabeçote de metal" }, problema.Extensions["itensNaoEncontrados"]);
        Assert.Equal("6703f1c2a9", problema.Extensions["projetoId"]);
    }

    [Fact]
    public void Esclarecimento_vira_422_com_pergunta_e_sugestoes()
    {
        SugestaoMaterial[] sugestoes = [new("cantoneira", "id-7", "Suporte em L de aço", OrigemSugestao.Agente, null)];

        var problema = MapeadorDeErros.Mapear(new EsclarecimentoNecessarioException("p1", "Confirma?", sugestoes));

        Assert.Equal(422, problema.Status);
        Assert.Equal("ESCLARECIMENTO_NECESSARIO", problema.Extensions["code"]);
        Assert.Equal("Confirma?", problema.Extensions["pergunta"]);
        Assert.Same(sugestoes, problema.Extensions["sugestoes"]);
        Assert.Equal("p1", problema.Extensions["projetoId"]);
    }

    [Fact]
    public void Projeto_arquivado_vira_422_com_a_mensagem_da_RN11()
    {
        var problema = MapeadorDeErros.Mapear(new ProjetoArquivadoException());

        Assert.Equal(422, problema.Status);
        Assert.Equal("PROJETO_ARQUIVADO", problema.Extensions["code"]);
        Assert.Equal("Este projeto está inativo.", problema.Title);
    }

    [Fact]
    public void Falha_de_processamento_vira_500_com_a_mensagem_fixa_sem_detalhes_da_causa()
    {
        var problema = MapeadorDeErros.Mapear(new FalhaProcessamentoException(new TimeoutException("segredo interno")));

        Assert.Equal(500, problema.Status);
        Assert.Equal("FALHA_PROCESSAMENTO", problema.Extensions["code"]);
        Assert.Equal(FalhaProcessamentoException.MensagemFixa, problema.Title);
        Assert.Null(problema.Detail);
    }

    [Fact]
    public void Duplicidade_vira_409_e_recurso_inexistente_vira_404()
    {
        var duplicado = MapeadorDeErros.Mapear(new MaterialDuplicadoException("placa"));
        var inexistente = MapeadorDeErros.Mapear(new RecursoNaoEncontradoException("Material", "x"));

        Assert.Equal(409, duplicado.Status);
        Assert.Equal("MATERIAL_DUPLICADO", duplicado.Extensions["code"]);
        Assert.Equal("placa", duplicado.Extensions["termoDuplicado"]);
        Assert.Equal(404, inexistente.Status);
        Assert.Equal("RECURSO_NAO_ENCONTRADO", inexistente.Extensions["code"]);
    }

    [Fact]
    public void Requisicao_malformada_vira_400()
    {
        var problema = MapeadorDeErros.Mapear(new BadHttpRequestException("JSON inválido"));

        Assert.Equal(400, problema.Status);
        Assert.Equal("VALIDACAO", problema.Extensions["code"]);
    }

    [Fact]
    public void Erro_inesperado_vira_500_sem_expor_a_mensagem_original()
    {
        var problema = MapeadorDeErros.Mapear(new InvalidOperationException("string de conexão com senha"));

        Assert.Equal(500, problema.Status);
        Assert.Equal("ERRO_INTERNO", problema.Extensions["code"]);
        Assert.DoesNotContain("senha", problema.Title, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(400, "VALIDACAO", "Requisição inválida.")]
    [InlineData(401, "NAO_AUTENTICADO", "Não autenticado.")]
    [InlineData(403, "ACESSO_NEGADO", "Acesso negado.")]
    [InlineData(404, "RECURSO_NAO_ENCONTRADO", "Recurso não encontrado.")]
    [InlineData(500, "ERRO_INTERNO", "Erro interno no servidor.")]
    public void Erros_gerados_pelo_ASPNET_ganham_code_type_e_titulo_em_portugues(int status, string codigo, string titulo)
    {
        var problema = new ProblemDetails { Status = status, Title = "Unauthorized" };

        MapeadorDeErros.CompletarCodigo(problema);

        Assert.Equal(codigo, problema.Extensions["code"]);
        Assert.Equal(CodigosErro.Tipo(codigo), problema.Type);
        Assert.Equal(titulo, problema.Title);
    }

    [Fact]
    public void Nao_sobrescreve_code_existente_nem_inventa_code_para_status_sem_mapeamento()
    {
        var comCodigo = MapeadorDeErros.Mapear(new ProjetoArquivadoException());
        var semMapeamento = new ProblemDetails { Status = StatusCodes.Status405MethodNotAllowed };
        var semStatus = new ProblemDetails();

        MapeadorDeErros.CompletarCodigo(comCodigo);
        MapeadorDeErros.CompletarCodigo(semMapeamento);
        MapeadorDeErros.CompletarCodigo(semStatus);

        Assert.Equal("PROJETO_ARQUIVADO", comCodigo.Extensions["code"]);
        Assert.False(semMapeamento.Extensions.ContainsKey("code"));
        Assert.False(semStatus.Extensions.ContainsKey("code"));
    }
}
