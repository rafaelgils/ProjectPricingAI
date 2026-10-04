namespace ProjectPricing.Dominio.Excecoes;

/// <summary>Há termo sem material correspondente e sem sugestão do agente (RN03, RN09).</summary>
public sealed class ItensNaoEncontradosException : ExcecaoDeDominio
{
    public const string CodigoErro = "ITENS_NAO_ENCONTRADOS";

    public ItensNaoEncontradosException(string projetoId, IReadOnlyList<string> itensNaoEncontrados)
        : base(CodigoErro, "Itens não encontrados no catálogo")
    {
        ProjetoId = projetoId;
        ItensNaoEncontrados = itensNaoEncontrados;
    }

    public string ProjetoId { get; }

    public IReadOnlyList<string> ItensNaoEncontrados { get; }
}
