using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectPricing.Api.Json;

/// <summary>
/// Datas saem em ISO 8601 no fuso do negócio (standards.md §5, plano P3): 2026-10-04T14:30:00-03:00.
/// No banco elas ficam em UTC.
/// </summary>
public sealed class ConversorDataComFuso(TimeZoneInfo fuso) : JsonConverter<DateTimeOffset>
{
    public const string FusoPadrao = "America/Sao_Paulo";
    private const string Formato = "yyyy-MM-dd'T'HH:mm:sszzz";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(TimeZoneInfo.ConvertTime(value, fuso).ToString(Formato, CultureInfo.InvariantCulture));
    }
}
