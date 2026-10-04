using System.Reflection;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace ProjectPricing.Infraestrutura.Mongo;

/// <summary>
/// Grava o enum com o mesmo código que a API devolve em JSON (<see cref="JsonStringEnumMemberNameAttribute"/>),
/// para banco, API e documentação usarem um único código (ex.: <c>m2</c>, <c>cotado</c>).
/// </summary>
public sealed class SerializadorEnumPorCodigo<TEnum> : StructSerializerBase<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> CodigoPorValor = Enum.GetValues<TEnum>()
        .ToDictionary(valor => valor, ObterCodigo);

    private static readonly Dictionary<string, TEnum> ValorPorCodigo = CodigoPorValor
        .ToDictionary(par => par.Value, par => par.Key, StringComparer.Ordinal);

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TEnum value)
    {
        context.Writer.WriteString(CodigoPorValor[value]);
    }

    public override TEnum Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var codigo = context.Reader.ReadString();

        return ValorPorCodigo.TryGetValue(codigo, out var valor)
            ? valor
            : throw new FormatException($"Código \"{codigo}\" inválido para {typeof(TEnum).Name}.");
    }

    private static string ObterCodigo(TEnum valor)
    {
        var nome = valor.ToString();
        var atributo = typeof(TEnum).GetField(nome)?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();

        return atributo?.Name ?? nome;
    }
}

/// <summary>Aplica o <see cref="SerializadorEnumPorCodigo{TEnum}"/> a todos os enums do domínio.</summary>
public sealed class ProvedorSerializacaoEnumsDoDominio : IBsonSerializationProvider
{
    private static readonly Assembly AssemblyDominio = typeof(Dominio.Materiais.Material).Assembly;

    public IBsonSerializer? GetSerializer(Type type)
    {
        if (!type.IsEnum || type.Assembly != AssemblyDominio)
        {
            return null;
        }

        var tipoSerializador = typeof(SerializadorEnumPorCodigo<>).MakeGenericType(type);
        return (IBsonSerializer)Activator.CreateInstance(tipoSerializador)!;
    }
}
