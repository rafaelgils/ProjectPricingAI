using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Dominio.Excecoes;

/// <summary>Falta medida ou quantidade (RN06) ou há material a confirmar (RN09).</summary>
public sealed class EsclarecimentoNecessarioException : ExcecaoDeDominio
{
    public const string CodigoErro = "ESCLARECIMENTO_NECESSARIO";

    public EsclarecimentoNecessarioException(
        string projetoId,
        string pergunta,
        IReadOnlyList<SugestaoMaterial> sugestoes)
        : base(CodigoErro, "Esclarecimento necessário")
    {
        ProjetoId = projetoId;
        Pergunta = pergunta;
        Sugestoes = sugestoes;
    }

    public string ProjetoId { get; }

    public string Pergunta { get; }

    public IReadOnlyList<SugestaoMaterial> Sugestoes { get; }
}
