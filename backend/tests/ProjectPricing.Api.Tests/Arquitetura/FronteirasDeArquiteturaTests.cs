using System.Reflection;
using ProjectPricing.Aplicacao.Usuarios;
using ProjectPricing.Dominio.Materiais;
using ProjectPricing.Infraestrutura.Repositorios;

namespace ProjectPricing.Api.Tests.Arquitetura;

/// <summary>
/// Testes de arquitetura (ADR-014): a regra de dependência entre camadas e as fronteiras entre os módulos do
/// monolito modular (Catálogo, Cotação, Identidade) são verificadas por reflexão, sem biblioteca extra.
/// Um desvio quebra o build em vez de virar dívida silenciosa.
/// </summary>
public class FronteirasDeArquiteturaTests
{
    private static readonly Assembly Dominio = typeof(Material).Assembly;
    private static readonly Assembly Aplicacao = typeof(IUsuarioAtual).Assembly;
    private static readonly Assembly Infraestrutura = typeof(MaterialRepository).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void Dominio_nao_depende_de_nenhuma_outra_camada_nem_de_infraestrutura_externa()
    {
        var proibidos = new[] { "ProjectPricing.Aplicacao", "ProjectPricing.Infraestrutura", "ProjectPricing.Api", "MongoDB", "Anthropic", "Microsoft.AspNetCore", "ModelContextProtocol" };

        Assert.Empty(Referencias(Dominio, proibidos));
    }

    [Fact]
    public void Aplicacao_nao_depende_de_infraestrutura_nem_da_api()
    {
        var proibidos = new[] { "ProjectPricing.Infraestrutura", "ProjectPricing.Api", "MongoDB", "Anthropic", "Microsoft.AspNetCore" };

        Assert.Empty(Referencias(Aplicacao, proibidos));
    }

    [Fact]
    public void Infraestrutura_nao_depende_da_api()
    {
        Assert.Empty(Referencias(Infraestrutura, ["ProjectPricing.Api"]));
    }

    [Theory]
    [InlineData("ProjectPricing.Aplicacao.Materiais", new[] { "ProjectPricing.Aplicacao.Projetos", "ProjectPricing.Aplicacao.Agente", "ProjectPricing.Aplicacao.Cotacao" })]
    [InlineData("ProjectPricing.Aplicacao.Usuarios", new[] { "ProjectPricing.Aplicacao.Materiais", "ProjectPricing.Aplicacao.Projetos", "ProjectPricing.Aplicacao.Agente", "ProjectPricing.Aplicacao.Cotacao" })]
    [InlineData("ProjectPricing.Aplicacao.Projetos", new[] { "ProjectPricing.Aplicacao.Materiais", "ProjectPricing.Aplicacao.Agente", "ProjectPricing.Aplicacao.Cotacao" })]
    [InlineData("ProjectPricing.Aplicacao.Cotacao", new[] { "ProjectPricing.Aplicacao.Materiais", "ProjectPricing.Aplicacao.Projetos", "ProjectPricing.Aplicacao.Agente" })]
    [InlineData("ProjectPricing.Aplicacao.Agente", new[] { "ProjectPricing.Aplicacao.Materiais", "ProjectPricing.Aplicacao.Projetos" })]
    public void Modulo_da_aplicacao_nao_acessa_modulos_proibidos(string modulo, string[] proibidos)
    {
        var tipos = TiposDoModulo(Aplicacao, modulo).ToList();
        Assert.NotEmpty(tipos);

        var violacoes = tipos
            .SelectMany(tipo => TiposUsadosNaSuperficie(tipo).Select(usado => (tipo, usado)))
            .Where(par => proibidos.Any(p => EstaNoNamespace(par.usado, p)))
            .Select(par => $"{par.tipo.FullName} → {par.usado.FullName}")
            .Distinct()
            .ToList();

        Assert.Empty(violacoes);
    }

    [Fact]
    public void O_detector_enxerga_dependencias_reais_entre_modulos()
    {
        // Guarda contra testes vazios: o agente de fato usa as ferramentas do módulo Cotação.
        var usados = TiposUsadosNaSuperficie(typeof(ProjectPricing.Aplicacao.Agente.AgenteProjetos));

        Assert.Contains(usados, t => EstaNoNamespace(t, "ProjectPricing.Aplicacao.Cotacao"));
    }

    [Fact]
    public void Entidades_do_dominio_nao_conhecem_a_serializacao_do_banco()
    {
        var comAtributosDoMongo = Dominio.GetTypes()
            .Where(t => t.GetCustomAttributes(inherit: false).Any(a => a.GetType().Namespace?.StartsWith("MongoDB", StringComparison.Ordinal) == true))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(comAtributosDoMongo);
    }

    private static IEnumerable<string> Referencias(Assembly assembly, IEnumerable<string> prefixosProibidos) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(nome => prefixosProibidos.Any(p => nome.StartsWith(p, StringComparison.Ordinal)));

    private static IEnumerable<Type> TiposDoModulo(Assembly assembly, string modulo) =>
        assembly.GetTypes().Where(t => EstaNoNamespace(t, modulo) && !t.IsNested);

    private static bool EstaNoNamespace(Type tipo, string ns) =>
        tipo.Namespace is { } atual && (atual == ns || atual.StartsWith(ns + ".", StringComparison.Ordinal));

    /// <summary>Tipos que um tipo expõe: construtores, métodos, propriedades e campos (a dependência que o DI enxerga).</summary>
    private static IEnumerable<Type> TiposUsadosNaSuperficie(Type tipo)
    {
        const BindingFlags Todos = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        var usados = tipo.GetConstructors(Todos).SelectMany(c => c.GetParameters().Select(p => p.ParameterType))
            .Concat(tipo.GetMethods(Todos).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
            .Concat(tipo.GetProperties(Todos).Select(p => p.PropertyType))
            .Concat(tipo.GetFields(Todos).Select(f => f.FieldType))
            .Concat(tipo.GetInterfaces());

        return usados.SelectMany(Desembrulhar).Distinct();
    }

    /// <summary>Task&lt;T&gt;, IReadOnlyList&lt;T&gt;, T[]: o que importa é o T.</summary>
    private static IEnumerable<Type> Desembrulhar(Type tipo)
    {
        if (tipo.HasElementType)
        {
            return Desembrulhar(tipo.GetElementType()!);
        }

        return tipo.IsGenericType
            ? tipo.GetGenericArguments().SelectMany(Desembrulhar).Append(tipo.GetGenericTypeDefinition())
            : [tipo];
    }
}
