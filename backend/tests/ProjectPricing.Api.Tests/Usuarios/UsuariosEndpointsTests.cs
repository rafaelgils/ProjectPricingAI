using Moq;
using ProjectPricing.Api.Usuarios;
using ProjectPricing.Aplicacao.Usuarios;

namespace ProjectPricing.Api.Tests.Usuarios;

public class UsuariosEndpointsTests
{
    [Fact]
    public void Perfil_devolve_id_nome_email_e_papeis()
    {
        var usuario = Mock.Of<IUsuarioAtual>(u =>
            u.KeycloakId == "kc-1"
            && u.Nome == "Ana Souza"
            && u.Email == "ana@exemplo.local"
            && u.Papeis == new[] { Papeis.ClienteInterno });

        var resposta = UsuariosEndpoints.ObterPerfil(usuario);

        Assert.Equal(200, resposta.StatusCode);
        var perfil = Assert.IsType<PerfilUsuarioResposta>(resposta.Value);
        Assert.Equal("kc-1", perfil.KeycloakId);
        Assert.Equal("Ana Souza", perfil.Nome);
        Assert.Equal("ana@exemplo.local", perfil.Email);
        Assert.Equal([Papeis.ClienteInterno], perfil.Papeis);
    }
}
