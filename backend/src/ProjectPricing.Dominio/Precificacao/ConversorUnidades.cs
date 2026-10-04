using ProjectPricing.Dominio.Comum;

namespace ProjectPricing.Dominio.Precificacao;

public interface IConversorUnidades
{
    /// <summary>Quantidade na unidade do material, ainda sem arredondamento (RN05).</summary>
    /// <exception cref="Excecoes.MedidaInvalidaException">A medida não pode ser convertida.</exception>
    decimal Converter(MedidaInformada medida, UnidadeMedida destino);
}

/// <summary>Escolhe a regra pela unidade do material. Uma unidade nova entra como nova regra (aberto/fechado).</summary>
public sealed class ConversorUnidades : IConversorUnidades
{
    private readonly Dictionary<UnidadeMedida, IRegraConversao> _regras;

    public ConversorUnidades(IEnumerable<IRegraConversao> regras)
    {
        _regras = regras.ToDictionary(r => r.Destino);
    }

    /// <summary>Todas as regras da tabela da RN05.</summary>
    public static ConversorUnidades CriarPadrao() => new(
    [
        new RegraMetro(),
        new RegraMetroQuadrado(),
        new RegraLitro(),
        new RegraHora(),
        new RegraUnidade(),
    ]);

    public decimal Converter(MedidaInformada medida, UnidadeMedida destino)
    {
        ArgumentNullException.ThrowIfNull(medida);

        return _regras.TryGetValue(destino, out var regra)
            ? regra.Converter(medida)
            : throw new InvalidOperationException($"Não há regra de conversão para {destino}.");
    }
}
