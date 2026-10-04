using System.Reflection;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using ProjectPricing.Dominio.Comum;

namespace ProjectPricing.Infraestrutura.Mongo;

/// <summary>Grava o enum com o mesmo código que a API devolve em JSON (<see cref="CodigosEnum{TEnum}"/>).</summary>
public sealed class SerializadorEnumPorCodigo<TEnum> : StructSerializerBase<TEnum>
    where TEnum : struct, Enum
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TEnum value)
    {
        context.Writer.WriteString(CodigosEnum<TEnum>.Codigo(value));
    }

    public override TEnum Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var codigo = context.Reader.ReadString();

        return CodigosEnum<TEnum>.TentarConverter(codigo, out var valor)
            ? valor
            : throw new FormatException($"Código \"{codigo}\" inválido para {typeof(TEnum).Name}.");
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
