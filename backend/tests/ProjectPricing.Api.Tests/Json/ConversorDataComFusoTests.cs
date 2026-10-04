using System.Text.Json;
using ProjectPricing.Api.Json;

namespace ProjectPricing.Api.Tests.Json;

public class ConversorDataComFusoTests
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        Converters = { new ConversorDataComFuso(TimeZoneInfo.FindSystemTimeZoneById(ConversorDataComFuso.FusoPadrao)) },
    };

    [Fact]
    public void Data_em_UTC_sai_no_fuso_de_Brasilia_no_formato_da_documentacao()
    {
        var utc = new DateTimeOffset(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);

        Assert.Equal("\"2026-10-04T14:30:00-03:00\"", JsonSerializer.Serialize(utc, Opcoes));
    }

    [Fact]
    public void Leitura_preserva_o_instante()
    {
        var lida = JsonSerializer.Deserialize<DateTimeOffset>("\"2026-10-04T14:30:00-03:00\"", Opcoes);

        Assert.Equal(new DateTimeOffset(2026, 10, 4, 17, 30, 0, TimeSpan.Zero), lida);
    }
}
