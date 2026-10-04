namespace ProjectPricing.Dominio.Excecoes;

/// <summary>Usuário ou e-mail já cadastrado no Keycloak.</summary>
public sealed class UsuarioDuplicadoException : ExcecaoDeDominio
{
    public const string CodigoErro = "USUARIO_DUPLICADO";

    public UsuarioDuplicadoException()
        : base(CodigoErro, "Já existe um usuário com esse nome de usuário ou e-mail.")
    {
    }
}

/// <summary>
/// O Admin não pode desativar a si mesmo nem tirar o próprio papel de admin: o sistema poderia ficar
/// sem ninguém para gerenciar usuários.
/// </summary>
public sealed class AlteracaoDoProprioUsuarioException : ExcecaoDeDominio
{
    public const string CodigoErro = "ALTERACAO_PROPRIO_USUARIO";

    public AlteracaoDoProprioUsuarioException()
        : base(CodigoErro, "Você não pode desativar o seu próprio usuário nem remover o seu papel de admin.")
    {
    }
}
