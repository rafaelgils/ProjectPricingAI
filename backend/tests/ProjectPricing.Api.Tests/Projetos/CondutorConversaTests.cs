using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectPricing.Api.Json;
using ProjectPricing.Api.Projetos;
using ProjectPricing.Aplicacao.Agente;
using ProjectPricing.Aplicacao.Projetos;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Excecoes;
using ProjectPricing.Dominio.Projetos;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace ProjectPricing.Api.Tests.Projetos;

/// <summary>Formato do stream (standards.md §5): delta imediato, cotacao ou erro, e sempre fim.</summary>
public class CondutorConversaTests
{
    private const string Id = "6703f1c2a900000000000001";
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);

    private readonly Mock<IAgenteProjetos> _agente = new();
    private readonly Projeto _projeto = NovoProjeto();

    [Fact]
    public async Task Cotacao_feita_envia_aviso_resposta_cotacao_e_fim()
    {
        _projeto.RegistrarCotacao([new ItemProjeto("m-chapa", "Chapa", 0.36m, UnidadeMedida.MetroQuadrado, 120.00m, 43.20m)], 43.20m, Agora);
        _agente
            .Setup(a => a.ProcessarAsync(_projeto, It.IsAny<Conversa>(), "Placa 60x60", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoAgente(_projeto, "Para esse projeto o valor estimado é R$ 43,20."));

        var (http, stream) = await Responder(StatusCodes.Status201Created, $"/api/v1/projetos/{Id}");

        Assert.Equal(201, http.Response.StatusCode);
        Assert.Equal("text/event-stream", http.Response.ContentType);
        Assert.Equal($"/api/v1/projetos/{Id}", http.Response.Headers.Location.ToString());
        Assert.Equal("no", http.Response.Headers["X-Accel-Buffering"].ToString());
        Assert.Equal(["delta", "delta", "cotacao", "fim"], Eventos(stream));
        Assert.Contains("""data: {"texto":"Analisando a descrição do projeto..."}""", stream, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"cotado\",\"itens\":[{\"materialId\":\"m-chapa\",\"nome\":\"Chapa\",\"quantidade\":0.36,\"unidade\":\"m2\",\"precoUnitario\":120.00,\"subtotal\":43.20}],\"valorTotal\":43.20,\"moeda\":\"BRL\",\"criadoEm\":\"2026-10-04T14:30:00-03:00\"", stream, StringComparison.Ordinal);
        Assert.EndsWith($"event: fim\ndata: {{\"projetoId\":\"{Id}\",\"status\":\"cotado\"}}\n\n", stream, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erro_de_negocio_vai_como_evento_em_Problem_Details()
    {
        _agente
            .Setup(a => a.ProcessarAsync(_projeto, It.IsAny<Conversa>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ItensNaoEncontradosException(Id, ["cabeçote de metal"]));

        var (_, stream) = await Responder(StatusCodes.Status201Created, null);

        Assert.Equal(["delta", "erro", "fim"], Eventos(stream));
        Assert.Contains("\"status\":422", stream, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"ITENS_NAO_ENCONTRADOS\"", stream, StringComparison.Ordinal);
        Assert.Contains("\"itensNaoEncontrados\":[\"cabeçote de metal\"]", stream, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"rascunho\"}", stream, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erro_inesperado_vira_500_sem_detalhes_internos()
    {
        _agente
            .Setup(a => a.ProcessarAsync(_projeto, It.IsAny<Conversa>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("detalhe interno"));

        var (_, stream) = await Responder(StatusCodes.Status200OK, null);

        Assert.Contains("\"code\":\"ERRO_INTERNO\"", stream, StringComparison.Ordinal);
        Assert.DoesNotContain("detalhe interno", stream, StringComparison.Ordinal);
        Assert.Equal("fim", Eventos(stream).Last());
    }

    [Fact]
    public async Task Cliente_que_desconecta_interrompe_o_stream_sem_erro()
    {
        using var cancelamento = new CancellationTokenSource();
        _agente
            .Setup(a => a.ProcessarAsync(_projeto, It.IsAny<Conversa>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(cancelamento.Cancel)
            .ThrowsAsync(new OperationCanceledException());

        var (_, stream) = await Responder(StatusCodes.Status200OK, null, cancelamento.Token);

        Assert.Equal(["delta"], Eventos(stream));
    }

    private async Task<(HttpContext Http, string Stream)> Responder(int status, string? location, CancellationToken cancellationToken = default)
    {
        var json = new JsonOptions();
        json.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        json.SerializerOptions.Converters.Add(new ConversorDataComFuso(TimeZoneInfo.FindSystemTimeZoneById(ConversorDataComFuso.FusoPadrao)));
        var condutor = new CondutorConversa(_agente.Object, Options.Create(json), NullLogger<CondutorConversa>.Instance);
        var http = new DefaultHttpContext();
        using var corpo = new MemoryStream();
        http.Response.Body = corpo;

        await condutor.ResponderAsync(http.Response, status, location, new ProjetoEmConversa(_projeto, new Conversa(Id)),
            "Placa 60x60", CondutorConversa.AvisoDescricao, cancellationToken);

        return (http, Encoding.UTF8.GetString(corpo.ToArray()));
    }

    private static List<string> Eventos(string stream) =>
        [.. stream.Split('\n').Where(l => l.StartsWith("event: ", StringComparison.Ordinal)).Select(l => l["event: ".Length..])];

    private static Projeto NovoProjeto()
    {
        var projeto = new Projeto("dono", "Placa", Agora);
        typeof(Projeto).GetProperty(nameof(Projeto.Id))!.SetValue(projeto, Id);
        return projeto;
    }
}
