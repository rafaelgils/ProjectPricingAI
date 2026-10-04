using Moq;
using ProjectPricing.Api.Usuarios;
using ProjectPricing.Api.Validacao;
using ProjectPricing.Aplicacao.Usuarios;

namespace ProjectPricing.Api.Tests.Usuarios;

public class GestaoUsuariosApiTests
{
    private const string Id = "6f1c2d9e-0000-4000-8000-000000000001";
    private static readonly DateTimeOffset Criacao = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);
    private static readonly UsuarioGerenciado Usuario = new(
        Id, "maria", "Maria", "Silva", "maria@empresa.com", Papeis.ClienteInterno, true, Criacao);
    private static readonly CriarUsuarioRequisicao NovoValido = new(
        "Maria.Silva", " Maria ", "Silva", "maria@empresa.com", Papeis.ClienteInterno, "Temp@1234");

    private readonly Mock<IGestaoIdentidade> _identidade = new();
    private readonly ServicoUsuarios _servico;

    static GestaoUsuariosApiTests()
    {
        ConfiguracaoValidacao.Aplicar();
    }

    public GestaoUsuariosApiTests()
    {
        _identidade.Setup(i => i.ObterAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(Usuario);
        _servico = new ServicoUsuarios(_identidade.Object, Mock.Of<IUsuarioAtual>(u => u.KeycloakId == "admin-logado"));
    }

    [Fact]
    public async Task Criar_responde_201_com_Location_e_normaliza_o_usuario()
    {
        _identidade.Setup(i => i.CriarAsync(It.IsAny<DadosNovoUsuario>(), It.IsAny<CancellationToken>())).ReturnsAsync(Id);

        var resposta = await GestaoUsuariosEndpoints.Criar(NovoValido, _servico, CancellationToken.None);

        Assert.Equal(201, resposta.StatusCode);
        Assert.Equal($"/api/v1/usuarios/{Id}", resposta.Location);
        Assert.Equal(Papeis.ClienteInterno, resposta.Value!.Papel);
        _identidade.Verify(i => i.CriarAsync(
            It.Is<DadosNovoUsuario>(d => d.Usuario == "maria.silva" && d.Nome == "Maria"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Listar_usa_a_pagina_padrao()
    {
        _identidade.Setup(i => i.ListarAsync("mar", 1, ConsultaUsuarios.TamanhoPadrao, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginaDeUsuarios([Usuario], 1));

        var resposta = await GestaoUsuariosEndpoints.Listar(new ConsultaUsuarios("mar", null, null), _servico, CancellationToken.None);

        Assert.Equal(1, resposta.Value!.Pagina);
        Assert.Equal(1, resposta.Value.Total);
        Assert.Equal("maria", Assert.Single(resposta.Value.Itens).Usuario);
    }

    [Fact]
    public async Task Obter_alterar_e_desativar()
    {
        var obtido = await GestaoUsuariosEndpoints.Obter(Id, _servico, CancellationToken.None);
        var alterado = await GestaoUsuariosEndpoints.Alterar(
            Id, new AlterarUsuarioRequisicao("Maria", "Souza", "maria@empresa.com", Papeis.ClienteExterno, true), _servico, CancellationToken.None);
        var desativado = await GestaoUsuariosEndpoints.Desativar(Id, _servico, CancellationToken.None);

        Assert.Equal(Id, obtido.Value!.Id);
        Assert.Equal(200, alterado.StatusCode);
        Assert.Equal(204, desativado.StatusCode);
        _identidade.Verify(i => i.DefinirPapelAsync(Id, Papeis.ClienteExterno, It.IsAny<CancellationToken>()), Times.Once);
        _identidade.Verify(i => i.AtualizarAsync(Id, "Maria", "Silva", "maria@empresa.com", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Criacao_valida()
    {
        Assert.True(new CriarUsuarioValidador().Validate(NovoValido).IsValid);
    }

    [Fact]
    public void Criacao_sem_campos_aponta_todos_em_camelCase()
    {
        var resultado = new CriarUsuarioValidador().Validate(new CriarUsuarioRequisicao(null, null, null, null, null, null));

        Assert.Equal(
            ["email", "nome", "papel", "senhaTemporaria", "sobrenome", "usuario"],
            resultado.Errors.Select(e => e.PropertyName).Distinct().Order());
    }

    [Theory]
    [InlineData("ma", false)]
    [InlineData("maria silva", false)]
    [InlineData("maria_silva-2.x", true)]
    public void Nome_de_usuario_sem_espacos_e_com_3_caracteres(string usuario, bool valido)
    {
        Assert.Equal(valido, new CriarUsuarioValidador().Validate(NovoValido with { Usuario = usuario }).IsValid);
    }

    [Theory]
    [InlineData("gerente")]
    [InlineData("Admin")]
    public void Papel_fora_dos_tres_do_sistema_e_invalido(string papel)
    {
        var resultado = new AlterarUsuarioValidador().Validate(new AlterarUsuarioRequisicao("Maria", "Silva", "m@e.com", papel, true));

        Assert.Contains(resultado.Errors, e => e.PropertyName == "papel");
    }

    [Fact]
    public void Senha_temporaria_curta_e_email_invalido()
    {
        var resultado = new CriarUsuarioValidador().Validate(NovoValido with { SenhaTemporaria = "1234567", Email = "maria" });

        Assert.Equal(["email", "senhaTemporaria"], resultado.Errors.Select(e => e.PropertyName).Order());
    }

    [Fact]
    public void Alteracao_exige_a_situacao()
    {
        var resultado = new AlterarUsuarioValidador().Validate(new AlterarUsuarioRequisicao("Maria", "Silva", "m@e.com", Papeis.Admin, null));

        Assert.Equal("ativo", Assert.Single(resultado.Errors).PropertyName);
    }

    [Theory]
    [InlineData(0, null, false)]
    [InlineData(1, 101, false)]
    [InlineData(2, 100, true)]
    public void Consulta_limita_pagina_e_tamanho(int? pagina, int? tamanho, bool valida)
    {
        Assert.Equal(valida, new ConsultaUsuariosValidador().Validate(new ConsultaUsuarios(null, pagina, tamanho)).IsValid);
    }
}
