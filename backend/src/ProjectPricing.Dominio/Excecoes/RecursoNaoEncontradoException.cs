namespace ProjectPricing.Dominio.Excecoes;

/// <summary>Recurso inexistente ou de outro cliente: os dois casos respondem 404 (RN07).</summary>
public sealed class RecursoNaoEncontradoException : ExcecaoDeDominio
{
    public const string CodigoErro = "RECURSO_NAO_ENCONTRADO";

    public RecursoNaoEncontradoException(string recurso, string id)
        : base(CodigoErro, $"{recurso} \"{id}\" não encontrado.")
    {
    }
}
