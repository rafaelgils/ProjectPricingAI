namespace ProjectPricing.Dominio.Projetos;

/// <param name="ClienteId">Nulo traz os projetos de todos os clientes (visão do Admin, RN07).</param>
/// <param name="Status">Nulo traz todos os status.</param>
public sealed record FiltroProjetos(string? ClienteId, StatusProjeto? Status, int Pagina, int Tamanho);

public sealed record PaginaDeProjetos(IReadOnlyList<Projeto> Itens, long Total);

public interface IProjetoRepository
{
    Task<Projeto?> ObterPorIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>Mais recentes primeiro (pela última alteração).</summary>
    Task<PaginaDeProjetos> ListarAsync(FiltroProjetos filtro, CancellationToken cancellationToken);

    Task InserirAsync(Projeto projeto, CancellationToken cancellationToken);

    /// <summary>Grava só se a versão no banco ainda for a lida, e avança a versão.</summary>
    /// <exception cref="Excecoes.ConflitoDeEdicaoException">Outra gravação aconteceu antes.</exception>
    Task AtualizarAsync(Projeto projeto, CancellationToken cancellationToken);
}
