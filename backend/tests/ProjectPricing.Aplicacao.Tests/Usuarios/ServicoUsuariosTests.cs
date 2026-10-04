using Moq;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Excecoes;

namespace ProjectPricing.Aplicacao.Tests.Usuarios;

public class ServicoUsuariosTests
{
    private const string IdAdminLogado = "admin-logado";
    private const string Id = "usuario-1";
    private static readonly DateTimeOffset Criacao = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);
    private static readonly UsuarioGerenciado Usuario = new(
        Id, "maria", "Maria", "Silva", "maria@empresa.com", Papeis.ClienteInterno, true, Criacao);

    private readonly Mock<IGestaoIdentidade> _identidade = new();
    private readonly Mock<IUsuarioAtual> _usuarioAtual = new();

    public ServicoUsuariosTests()
    {
        _usuarioAtual.SetupGet(u => u.KeycloakId).Returns(IdAdminLogado);
        _identidade.Setup(i => i.ObterAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(Usuario);
    }

    private ServicoUsuarios Servico => new(_identidade.Object, _usuarioAtual.Object);

    [Fact]
    public async Task Criar_cria_define_o_papel_e_devolve_o_usuario()
    {
        var dados = new DadosNovoUsuario("maria", "Maria", "Silva", "maria@empresa.com", Papeis.ClienteInterno, "Temp@1234");
        _identidade.Setup(i => i.CriarAsync(dados, It.IsAny<CancellationToken>())).ReturnsAsync(Id);

        var criado = await Servico.CriarAsync(dados, CancellationToken.None);

        Assert.Same(Usuario, criado);
        _identidade.Verify(i => i.DefinirPapelAsync(Id, Papeis.ClienteInterno, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Obter_inexistente_lanca_recurso_nao_encontrado()
    {
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Servico.ObterAsync("nao-existe", CancellationToken.None));
    }

    [Fact]
    public async Task Alterar_atualiza_dados_e_papel()
    {
        var dados = new DadosAlteracaoUsuario("Maria", "Souza", "maria@empresa.com", Papeis.ClienteExterno, false);

        await Servico.AlterarAsync(Id, dados, CancellationToken.None);

        _identidade.Verify(i => i.AtualizarAsync(Id, "Maria", "Souza", "maria@empresa.com", false, CancellationToken.None), Times.Once);
        _identidade.Verify(i => i.DefinirPapelAsync(Id, Papeis.ClienteExterno, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Alterar_inexistente_nao_chama_o_keycloak()
    {
        var dados = new DadosAlteracaoUsuario("Maria", "Souza", "maria@empresa.com", Papeis.Admin, true);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Servico.AlterarAsync("nao-existe", dados, CancellationToken.None));

        _identidade.Verify(i => i.AtualizarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(Papeis.Admin, false)]
    [InlineData(Papeis.ClienteInterno, true)]
    public async Task Admin_nao_pode_se_desativar_nem_perder_o_papel_admin(string papel, bool ativo)
    {
        _identidade.Setup(i => i.ObterAsync(IdAdminLogado, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Usuario with { Id = IdAdminLogado, Papel = Papeis.Admin });
        var dados = new DadosAlteracaoUsuario("Ana", "Admin", "ana@empresa.com", papel, ativo);

        var erro = await Assert.ThrowsAsync<AlteracaoDoProprioUsuarioException>(
            () => Servico.AlterarAsync(IdAdminLogado, dados, CancellationToken.None));

        Assert.Equal("ALTERACAO_PROPRIO_USUARIO", erro.Codigo);
        _identidade.Verify(i => i.DefinirPapelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_pode_alterar_o_proprio_nome()
    {
        _identidade.Setup(i => i.ObterAsync(IdAdminLogado, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Usuario with { Id = IdAdminLogado, Papel = Papeis.Admin });

        await Servico.AlterarAsync(IdAdminLogado, new DadosAlteracaoUsuario("Ana", "Lima", "ana@empresa.com", Papeis.Admin, true), CancellationToken.None);

        _identidade.Verify(i => i.AtualizarAsync(IdAdminLogado, "Ana", "Lima", "ana@empresa.com", true, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Desativar_mantem_os_dados_e_so_desliga_o_usuario()
    {
        await Servico.DesativarAsync(Id, CancellationToken.None);

        _identidade.Verify(i => i.AtualizarAsync(Id, "Maria", "Silva", "maria@empresa.com", false, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Admin_nao_pode_desativar_a_si_mesmo()
    {
        await Assert.ThrowsAsync<AlteracaoDoProprioUsuarioException>(() => Servico.DesativarAsync(IdAdminLogado, CancellationToken.None));
    }
}
