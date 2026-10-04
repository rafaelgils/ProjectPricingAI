using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectPricing.Api.Autorizacao;
using ProjectPricing.Api.Materiais;
using ProjectPricing.Api.Validacao;
using ProjectPricing.Aplicacao.Usuarios;

namespace ProjectPricing.Api.Usuarios;

public sealed record CriarUsuarioRequisicao(
    string? Usuario,
    string? Nome,
    string? Sobrenome,
    string? Email,
    string? Papel,
    string? SenhaTemporaria)
{
    public DadosNovoUsuario ParaDados() =>
        new(Usuario!.Trim().ToLowerInvariant(), Nome!.Trim(), Sobrenome!.Trim(), Email!.Trim(), Papel!, SenhaTemporaria!);
}

public sealed record AlterarUsuarioRequisicao(string? Nome, string? Sobrenome, string? Email, string? Papel, bool? Ativo)
{
    public DadosAlteracaoUsuario ParaDados() => new(Nome!.Trim(), Sobrenome!.Trim(), Email!.Trim(), Papel!, Ativo!.Value);
}

public sealed record ConsultaUsuarios(string? Busca, int? Pagina, int? Tamanho)
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;
}

public sealed record UsuarioResposta(
    string Id,
    string Usuario,
    string Nome,
    string Sobrenome,
    string? Email,
    string? Papel,
    bool Ativo,
    DateTimeOffset CriadoEm)
{
    public static UsuarioResposta De(UsuarioGerenciado u) =>
        new(u.Id, u.Usuario, u.Nome, u.Sobrenome, u.Email, u.Papel, u.Ativo, u.CriadoEm);
}

internal static class RegrasUsuario
{
    public const int TamanhoMinimoSenha = 8;
    public const int TamanhoMaximoNome = 100;

    public static IRuleBuilderOptions<T, string?> PapelDoSistema<T>(this IRuleBuilder<T, string?> regra) =>
        regra.NotEmpty().WithMessage("Informe o papel.")
            .Must(papel => ServicoUsuarios.PapeisDoSistema.Contains(papel))
            .WithMessage("Papel inválido: use admin, cliente-interno ou cliente-externo.");

    public static IRuleBuilderOptions<T, string?> Nome<T>(this IRuleBuilder<T, string?> regra, string campo) =>
        regra.NotEmpty().WithMessage($"Informe o {campo}.")
            .MaximumLength(TamanhoMaximoNome).WithMessage($"O {campo} tem no máximo {TamanhoMaximoNome} caracteres.");

    public static IRuleBuilderOptions<T, string?> Email<T>(this IRuleBuilder<T, string?> regra) =>
        regra.NotEmpty().WithMessage("Informe o e-mail.")
            .EmailAddress().WithMessage("E-mail inválido.");
}

public sealed class CriarUsuarioValidador : AbstractValidator<CriarUsuarioRequisicao>
{
    public CriarUsuarioValidador()
    {
        RuleFor(r => r.Usuario)
            .NotEmpty().WithMessage("Informe o nome de usuário.")
            .Matches("^[a-zA-Z0-9._-]{3,50}$")
            .WithMessage("Use de 3 a 50 letras, números, ponto, hífen ou sublinhado, sem espaços.");
        RuleFor(r => r.Nome).Nome("nome");
        RuleFor(r => r.Sobrenome).Nome("sobrenome");
        RuleFor(r => r.Email).Email();
        RuleFor(r => r.Papel).PapelDoSistema();
        RuleFor(r => r.SenhaTemporaria)
            .NotEmpty().WithMessage("Informe a senha temporária.")
            .MinimumLength(RegrasUsuario.TamanhoMinimoSenha)
            .WithMessage($"A senha temporária tem no mínimo {RegrasUsuario.TamanhoMinimoSenha} caracteres.");
    }
}

public sealed class AlterarUsuarioValidador : AbstractValidator<AlterarUsuarioRequisicao>
{
    public AlterarUsuarioValidador()
    {
        RuleFor(r => r.Nome).Nome("nome");
        RuleFor(r => r.Sobrenome).Nome("sobrenome");
        RuleFor(r => r.Email).Email();
        RuleFor(r => r.Papel).PapelDoSistema();
        RuleFor(r => r.Ativo).NotNull().WithMessage("Informe se o usuário está ativo.");
    }
}

public sealed class ConsultaUsuariosValidador : AbstractValidator<ConsultaUsuarios>
{
    public ConsultaUsuariosValidador()
    {
        RuleFor(c => c.Pagina).GreaterThanOrEqualTo(1).When(c => c.Pagina is not null).WithMessage("A página começa em 1.");
        RuleFor(c => c.Tamanho)
            .InclusiveBetween(1, ConsultaUsuarios.TamanhoMaximo).When(c => c.Tamanho is not null)
            .WithMessage($"O tamanho da página vai de 1 a {ConsultaUsuarios.TamanhoMaximo}.");
    }
}

/// <summary>Gestão de usuários pelo Admin (RF10): repasse à Keycloak Admin API (ADR-004).</summary>
public static class GestaoUsuariosEndpoints
{
    public const string Caminho = "/api/v1/usuarios";

    /// <summary>Mapeia as rotas no grupo /usuarios, que já tem o /me para todos os papéis.</summary>
    public static RouteGroupBuilder MapearGestaoDeUsuarios(this RouteGroupBuilder usuarios)
    {
        var admin = usuarios.MapGroup("").RequireAuthorization(Politicas.PodeGerenciarUsuarios);

        admin.MapGet("/", Listar).Validar<ConsultaUsuarios>().WithSummary("Lista os usuários (Admin)");
        admin.MapGet("/{id}", Obter)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Detalhe do usuário (Admin)");
        admin.MapPost("/", Criar).Validar<CriarUsuarioRequisicao>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Cria o usuário com senha temporária e papel (Admin)");
        admin.MapPut("/{id}", Alterar).Validar<AlterarUsuarioRequisicao>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Altera dados, papel e situação do usuário (Admin)");
        admin.MapDelete("/{id}", Desativar)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Desativa o usuário; nada é apagado (Admin)");

        return usuarios;
    }

    public static async Task<Ok<PaginaResposta<UsuarioResposta>>> Listar(
        [AsParameters] ConsultaUsuarios consulta,
        ServicoUsuarios servico,
        CancellationToken cancellationToken)
    {
        var pagina = consulta.Pagina ?? 1;
        var tamanho = consulta.Tamanho ?? ConsultaUsuarios.TamanhoPadrao;
        var resultado = await servico.ListarAsync(consulta.Busca, pagina, tamanho, cancellationToken);

        return TypedResults.Ok(new PaginaResposta<UsuarioResposta>(
            [.. resultado.Itens.Select(UsuarioResposta.De)], pagina, tamanho, resultado.Total));
    }

    public static async Task<Ok<UsuarioResposta>> Obter(string id, ServicoUsuarios servico, CancellationToken cancellationToken) =>
        TypedResults.Ok(UsuarioResposta.De(await servico.ObterAsync(id, cancellationToken)));

    public static async Task<Created<UsuarioResposta>> Criar(
        CriarUsuarioRequisicao? requisicao,
        ServicoUsuarios servico,
        CancellationToken cancellationToken)
    {
        var usuario = await servico.CriarAsync(requisicao!.ParaDados(), cancellationToken);
        return TypedResults.Created($"{Caminho}/{usuario.Id}", UsuarioResposta.De(usuario));
    }

    public static async Task<Ok<UsuarioResposta>> Alterar(
        string id,
        AlterarUsuarioRequisicao? requisicao,
        ServicoUsuarios servico,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(UsuarioResposta.De(await servico.AlterarAsync(id, requisicao!.ParaDados(), cancellationToken)));

    public static async Task<NoContent> Desativar(string id, ServicoUsuarios servico, CancellationToken cancellationToken)
    {
        await servico.DesativarAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
