using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Aplicacao.Projetos;

/// <summary>Projeto e a conversa dele, prontos para o agente processar uma mensagem.</summary>
public sealed record ProjetoEmConversa(Projeto Projeto, Conversa Conversa);

/// <summary>Casos de uso de projetos e conversas (RF03, RF04, RF07, RN04, RN07, RN11).</summary>
public sealed class ServicoProjetos(
    IProjetoRepository projetos,
    IConversaRepository conversas,
    IUsuarioAtual usuario,
    TimeProvider relogio)
{
    private const string Recurso = "Projeto";

    /// <summary>Cria o projeto em rascunho, do usuário logado, com a conversa vazia (relação 1:1).</summary>
    public async Task<ProjetoEmConversa> CriarAsync(string descricao, CancellationToken cancellationToken)
    {
        var projeto = new Projeto(usuario.KeycloakId, descricao, relogio.GetUtcNow());
        await projetos.InserirAsync(projeto, cancellationToken);

        var conversa = new Conversa(projeto.Id!);
        await conversas.InserirAsync(conversa, cancellationToken);
        return new ProjetoEmConversa(projeto, conversa);
    }

    /// <summary>RN07: o cliente vê só os próprios projetos; o Admin vê todos.</summary>
    public async Task<Projeto> ObterAsync(string id, CancellationToken cancellationToken) =>
        await ObterVisivelAsync(id, apenasDono: false, cancellationToken);

    public Task<PaginaDeProjetos> ListarAsync(
        StatusProjeto? status,
        int pagina,
        int tamanho,
        CancellationToken cancellationToken)
    {
        var clienteId = usuario.EhAdmin ? null : usuario.KeycloakId;
        return projetos.ListarAsync(new FiltroProjetos(clienteId, status, pagina, tamanho), cancellationToken);
    }

    /// <summary>Exclusão lógica (RN04), pelo dono ou pelo Admin.</summary>
    public async Task ArquivarAsync(string id, CancellationToken cancellationToken)
    {
        var projeto = await ObterVisivelAsync(id, apenasDono: false, cancellationToken);
        if (projeto.Status == StatusProjeto.Arquivado)
        {
            return;
        }

        projeto.Arquivar(relogio.GetUtcNow());
        await projetos.AtualizarAsync(projeto, cancellationToken);
    }

    /// <summary>
    /// Mensagem de refinamento: só o dono, e nunca em projeto arquivado (RN11). Validado antes de abrir o stream.
    /// </summary>
    public async Task<ProjetoEmConversa> PrepararMensagemAsync(string id, CancellationToken cancellationToken)
    {
        var projeto = await ObterVisivelAsync(id, apenasDono: true, cancellationToken);
        projeto.GarantirQueNaoEstaArquivado();
        return new ProjetoEmConversa(projeto, await ObterConversaAsync(projeto, cancellationToken));
    }

    /// <summary>Histórico visível ao cliente: mensagens do cliente e do assistente, sem os registros internos.</summary>
    public async Task<IReadOnlyList<Mensagem>> HistoricoAsync(string id, CancellationToken cancellationToken)
    {
        var projeto = await ObterVisivelAsync(id, apenasDono: false, cancellationToken);
        var conversa = await ObterConversaAsync(projeto, cancellationToken);
        return [.. conversa.Mensagens.Where(m => m.Papel != PapelMensagem.Tool)];
    }

    private async Task<Projeto> ObterVisivelAsync(string id, bool apenasDono, CancellationToken cancellationToken)
    {
        var projeto = await projetos.ObterPorIdAsync(id, cancellationToken);
        var visivel = projeto is not null
            && (projeto.ClienteId == usuario.KeycloakId || (!apenasDono && usuario.EhAdmin));

        return visivel ? projeto! : throw new RecursoNaoEncontradoException(Recurso, id);
    }

    private async Task<Conversa> ObterConversaAsync(Projeto projeto, CancellationToken cancellationToken) =>
        await conversas.ObterPorProjetoIdAsync(projeto.Id!, cancellationToken)
        ?? throw new InvalidOperationException($"Projeto {projeto.Id} sem conversa.");
}
