using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using ProjectPricing.Dominio.Comum;
using ProjectPricing.Dominio.Conversas;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Dominio.Projetos;
using ProjectPricing.Infraestrutura.Mongo;

namespace ProjectPricing.Infraestrutura.Tests.Mongo;

/// <summary>Serialização em memória: nenhum teste acessa o MongoDB (standards.md §7).</summary>
public class MapeamentoMongoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 14, 30, 0, TimeSpan.FromHours(-3));

    public MapeamentoMongoTests()
    {
        MapeamentoMongo.Registrar();
    }

    [Fact]
    public void Registrar_pode_ser_chamado_mais_de_uma_vez()
    {
        var excecao = Record.Exception(MapeamentoMongo.Registrar);

        Assert.Null(excecao);
    }

    [Fact]
    public void Material_vira_documento_com_codigos_da_documentacao_e_Decimal128()
    {
        var material = new Material(
            "Película refletiva", ["tinta reflexiva"], TipoMaterial.Material, "Sinalização",
            UnidadeMedida.MetroQuadrado, 95.00m, "Refletivos SA", Agora);

        var documento = material.ToBsonDocument();

        Assert.Equal("Película refletiva", documento["nome"].AsString);
        Assert.Equal(new BsonArray { "tinta reflexiva" }, documento["sinonimos"].AsBsonArray);
        Assert.Equal("material", documento["tipo"].AsString);
        Assert.Equal("m2", documento["unidade"].AsString);
        Assert.Equal("ativo", documento["status"].AsString);
        Assert.Equal(BsonType.Decimal128, documento["precoUnitario"].BsonType);
        Assert.Equal(95.00m, documento["precoUnitario"].AsDecimal);
        Assert.Equal(BsonType.DateTime, documento["atualizadoEm"].BsonType);
        Assert.Equal(Agora.UtcDateTime, documento["atualizadoEm"].ToUniversalTime());
    }

    [Fact]
    public void Material_lido_do_banco_recebe_o_ObjectId_como_texto()
    {
        var documento = new Material(
            "Cabeçote de metal", [], TipoMaterial.Material, "Sinalização",
            UnidadeMedida.Unidade, 28.50m, "Metais LTDA", Agora).ToBsonDocument();
        documento["_id"] = ObjectId.Parse("6703e0aa0000000000000001");

        var material = BsonSerializer.Deserialize<Material>(documento);

        Assert.Equal("6703e0aa0000000000000001", material.Id);
        Assert.Equal(UnidadeMedida.Unidade, material.Unidade);
        Assert.Equal(28.50m, material.PrecoUnitario);
        Assert.Empty(material.Sinonimos);
        Assert.Equal(Agora, material.AtualizadoEm);
    }

    [Fact]
    public void Projeto_novo_grava_valor_total_nulo_e_status_rascunho()
    {
        var documento = new Projeto("keycloak-123", "Placa 60x60", Agora).ToBsonDocument();

        Assert.Equal("keycloak-123", documento["clienteId"].AsString);
        Assert.Equal("rascunho", documento["status"].AsString);
        Assert.Equal(BsonNull.Value, documento["valorTotal"]);
        Assert.Empty(documento["itens"].AsBsonArray);
        Assert.Equal(0, documento["versao"].AsInt32);
        Assert.Equal(BsonType.DateTime, documento["criadoEm"].BsonType);
        Assert.Equal(BsonType.DateTime, documento["alteradoEm"].BsonType);
    }

    [Fact]
    public void Projeto_cotado_e_lido_com_itens_e_valor_total()
    {
        var documento = new BsonDocument
        {
            ["_id"] = ObjectId.Parse("6703f1c2a900000000000001"),
            ["clienteId"] = "keycloak-123",
            ["descricao"] = "Placa 60x60",
            ["itens"] = new BsonArray { new ItemProjeto("6703e0aa0000000000000001", "Chapa", 0.36m, UnidadeMedida.MetroQuadrado, 120m, 43.20m).ToBsonDocument() },
            ["valorTotal"] = new BsonDecimal128(43.20m),
            ["status"] = "cotado",
            ["criadoEm"] = Agora.UtcDateTime,
            ["alteradoEm"] = Agora.UtcDateTime,
            ["versao"] = 2,
        };

        var projeto = BsonSerializer.Deserialize<Projeto>(documento);

        Assert.Equal("6703f1c2a900000000000001", projeto.Id);
        Assert.Equal(StatusProjeto.Cotado, projeto.Status);
        Assert.Equal(43.20m, projeto.ValorTotal);
        Assert.Equal(2, projeto.Versao);
        var item = Assert.Single(projeto.Itens);
        Assert.Equal("6703e0aa0000000000000001", item.MaterialId);
        Assert.Equal(0.36m, item.Quantidade);
    }

    [Fact]
    public void Item_grava_materialId_como_ObjectId()
    {
        var documento = new ItemProjeto("6703e0aa0000000000000001", "Chapa", 0.36m, UnidadeMedida.MetroQuadrado, 120m, 43.20m)
            .ToBsonDocument();

        Assert.Equal(BsonType.ObjectId, documento["materialId"].BsonType);
        Assert.Equal("Chapa", documento["nomeSnapshot"].AsString);
        Assert.Equal(BsonType.Decimal128, documento["precoUnitarioSnapshot"].BsonType);
        Assert.Equal(BsonType.Decimal128, documento["subtotal"].BsonType);
    }

    [Fact]
    public void Conversa_grava_projetoId_como_ObjectId_e_papel_da_mensagem_como_codigo()
    {
        var conversa = new Conversa("6703f1c2a900000000000001");
        conversa.AdicionarMensagem(new Mensagem(PapelMensagem.Usuario, "Quero cotar", Agora));

        var documento = conversa.ToBsonDocument();
        var lida = BsonSerializer.Deserialize<Conversa>(documento);

        Assert.Equal(BsonType.ObjectId, documento["projetoId"].BsonType);
        Assert.Equal("usuario", documento["mensagens"][0]["papel"].AsString);
        Assert.Equal("6703f1c2a900000000000001", lida.ProjetoId);
        Assert.Equal("Quero cotar", Assert.Single(lida.Mensagens).Conteudo);
    }

    [Fact]
    public void Codigo_de_enum_desconhecido_no_banco_gera_erro()
    {
        var documento = new BsonDocument { ["papel"] = "robo", ["conteudo"] = "x", ["enviadaEm"] = Agora.UtcDateTime };

        var erro = Assert.Throws<FormatException>(() => BsonSerializer.Deserialize<Mensagem>(documento));

        Assert.Contains("robo", erro.InnerException?.Message ?? erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Enums_fora_do_dominio_nao_usam_o_serializador_por_codigo()
    {
        var provedor = new ProvedorSerializacaoEnumsDoDominio();

        Assert.Null(provedor.GetSerializer(typeof(DayOfWeek)));
        Assert.Null(provedor.GetSerializer(typeof(string)));
        Assert.NotNull(provedor.GetSerializer(typeof(StatusProjeto)));
    }
}
