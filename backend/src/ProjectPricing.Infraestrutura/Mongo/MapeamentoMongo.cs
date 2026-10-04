using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Projetos;

namespace ProjectPricing.Infraestrutura.Mongo;

/// <summary>
/// Mapeamento das entidades para os documentos das coleções (ADR-003, business-rules.md §7).
/// O registro no driver é global; por isso roda uma única vez por processo.
/// </summary>
public static class MapeamentoMongo
{
    private const string NomePacotePadraoDoDriver = "__defaults__";
    private static readonly Lock Trava = new();
    private static bool _registrado;

    public static void Registrar()
    {
        lock (Trava)
        {
            if (_registrado)
            {
                return;
            }

            RegistrarConvencoesESerializadores();
            RegistrarClasses();
            _registrado = true;
        }
    }

    private static void RegistrarConvencoesESerializadores()
    {
        // As entidades são lidas pelo construtor privado sem parâmetros. A convenção de "tipo imutável"
        // faria o driver usar o construtor público, que valida e define valores iniciais (ex.: Status = Ativo).
        var padroesSemTipoImutavel = new ConventionPack();
        padroesSemTipoImutavel.AddRange(DefaultConventionPack.Instance.Conventions
            .Where(convencao => convencao is not ImmutableTypeClassMapConvention));
        ConventionRegistry.Remove(NomePacotePadraoDoDriver);
        ConventionRegistry.Register("ProjectPricing.Padroes", padroesSemTipoImutavel, _ => true);

        var convencoes = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
        };
        ConventionRegistry.Register(
            "ProjectPricing",
            convencoes,
            tipo => tipo.Namespace?.StartsWith("ProjectPricing.Dominio", StringComparison.Ordinal) == true);

        // Dinheiro e quantidades em Decimal128, nunca double (standards.md §3).
        BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
        // Datas como BSON date em UTC (plano, P3); o fuso -03:00 é aplicado só na API.
        BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
        BsonSerializer.RegisterSerializationProvider(new ProvedorSerializacaoEnumsDoDominio());
    }

    private static void RegistrarClasses()
    {
        var objectId = new StringSerializer(BsonType.ObjectId);

        BsonClassMap.RegisterClassMap<Material>(mapa =>
        {
            mapa.AutoMap();
            mapa.MapIdMember(m => m.Id).SetIdGenerator(StringObjectIdGenerator.Instance).SetSerializer(objectId);
        });

        BsonClassMap.RegisterClassMap<Projeto>(mapa =>
        {
            mapa.AutoMap();
            mapa.MapIdMember(p => p.Id).SetIdGenerator(StringObjectIdGenerator.Instance).SetSerializer(objectId);
        });

        BsonClassMap.RegisterClassMap<ItemProjeto>(mapa =>
        {
            mapa.AutoMap();
            mapa.MapMember(i => i.MaterialId).SetSerializer(objectId);
        });

        BsonClassMap.RegisterClassMap<Conversa>(mapa =>
        {
            mapa.AutoMap();
            mapa.MapIdMember(c => c.Id).SetIdGenerator(StringObjectIdGenerator.Instance).SetSerializer(objectId);
            mapa.MapMember(c => c.ProjetoId).SetSerializer(objectId);
        });

        BsonClassMap.RegisterClassMap<Mensagem>(mapa => mapa.AutoMap());
    }
}
