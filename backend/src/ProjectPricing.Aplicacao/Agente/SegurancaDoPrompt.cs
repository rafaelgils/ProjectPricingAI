using System.Text;

namespace ProjectPricing.Aplicacao.Agente;

/// <summary>
/// Defesas contra injeção de prompt (ADR-013). O texto do cliente e os nomes do catálogo entram em prompts
/// montados pelo código; aqui eles são reduzidos a uma única linha de texto comum, para que não consigam
/// criar linhas ou seções falsas ("## Itens atuais…") nem estourar o tamanho esperado.
/// </summary>
public static class SegurancaDoPrompt
{
    public const int TamanhoMaximoTermo = 200;
    public const int MaximoItensPorInterpretacao = 50;

    /// <summary>Medida acima disto não é um pedido real: ou é erro do LLM ou tentativa de estourar o cálculo.</summary>
    public const decimal MedidaMaxima = 1_000_000_000m;

    /// <summary>
    /// Troca quebras de linha e demais caracteres de controle por espaço, junta espaços repetidos e corta no limite.
    /// </summary>
    public static string Linha(string? texto, int tamanhoMaximo = TamanhoMaximoTermo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var limpo = new StringBuilder(Math.Min(texto.Length, tamanhoMaximo));
        var espacoPendente = false;
        foreach (var caractere in texto)
        {
            if (char.IsControl(caractere) || char.IsWhiteSpace(caractere))
            {
                espacoPendente = limpo.Length > 0;
                continue;
            }

            if (espacoPendente)
            {
                limpo.Append(' ');
                espacoPendente = false;
            }

            limpo.Append(caractere);
            if (limpo.Length >= tamanhoMaximo)
            {
                break;
            }
        }

        return limpo.ToString();
    }

    /// <summary>Limites da saída do LLM: itens em quantidade razoável, termos curtos e medidas dentro de uma faixa real.</summary>
    public static bool DentroDosLimites(InterpretacaoLlm interpretacao)
    {
        if (interpretacao.Itens.Count > MaximoItensPorInterpretacao)
        {
            return false;
        }

        return interpretacao.Itens.All(item =>
            item.Termo.Length <= TamanhoMaximoTermo
            && item.Unidade.Length <= 10
            && DentroDaFaixa(item.Quantidade)
            && DentroDaFaixa(item.Largura)
            && DentroDaFaixa(item.Altura));
    }

    private static bool DentroDaFaixa(decimal? medida) => medida is null || Math.Abs(medida.Value) <= MedidaMaxima;
}
