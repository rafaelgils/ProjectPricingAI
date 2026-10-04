using ProjectPricing.Dominio.Conversas;

namespace ProjectPricing.Dominio.Tests.Conversas;

public class ConversaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 14, 30, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Mensagens_sao_mantidas_na_ordem_de_envio()
    {
        var conversa = new Conversa("6703f1c2a900000000000001");
        var pergunta = new Mensagem(PapelMensagem.Usuario, "Quero cotar uma placa", Agora);
        var resposta = new Mensagem(PapelMensagem.Assistente, "Valor estimado: R$ 105,90", Agora.AddSeconds(3));

        conversa.AdicionarMensagem(pergunta);
        conversa.AdicionarMensagem(resposta);

        Assert.Null(conversa.Id);
        Assert.Equal("6703f1c2a900000000000001", conversa.ProjetoId);
        Assert.Equal([pergunta, resposta], conversa.Mensagens);
        Assert.Equal(PapelMensagem.Assistente, conversa.Mensagens[1].Papel);
        Assert.Equal("Valor estimado: R$ 105,90", conversa.Mensagens[1].Conteudo);
        Assert.Equal(Agora.AddSeconds(3), conversa.Mensagens[1].EnviadaEm);
    }

    [Fact]
    public void Conversa_exige_projeto()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Conversa(" "));
    }

    [Fact]
    public void Nao_aceita_mensagem_nula()
    {
        var conversa = new Conversa("projeto");

        Assert.Throws<ArgumentNullException>(() => conversa.AdicionarMensagem(null!));
        Assert.Throws<ArgumentNullException>(() => new Mensagem(PapelMensagem.Tool, null!, Agora));
    }
}
