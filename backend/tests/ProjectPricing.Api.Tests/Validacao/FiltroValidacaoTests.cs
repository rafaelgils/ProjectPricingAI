using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using ProjectPricing.Api.Materiais;
using ProjectPricing.Api.Validacao;

namespace ProjectPricing.Api.Tests.Validacao;

public class FiltroValidacaoTests
{
    private const string Seguiu = "handler executado";

    [Fact]
    public async Task Argumento_valido_segue_para_o_handler()
    {
        var resultado = await Executar(new ConsultaMateriais(null, null, null, 1, 20));

        Assert.Equal(Seguiu, resultado);
    }

    [Fact]
    public async Task Argumento_invalido_responde_400_com_erros_por_campo()
    {
        var resultado = await Executar(new ConsultaMateriais(null, null, null, 0, 20));

        var problema = Assert.IsType<ValidationProblem>(resultado);
        Assert.Equal(400, problema.StatusCode);
        Assert.True(problema.ProblemDetails.Errors.ContainsKey("pagina"));
    }

    [Fact]
    public async Task Corpo_ausente_responde_400()
    {
        var resultado = await Executar(argumento: null);

        var problema = Assert.IsType<ValidationProblem>(resultado);
        Assert.True(problema.ProblemDetails.Errors.ContainsKey(FiltroValidacao<ConsultaMateriais>.CampoCorpo));
    }

    private static async Task<object?> Executar(ConsultaMateriais? argumento)
    {
        ConfiguracaoValidacao.Aplicar();
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IValidator<ConsultaMateriais>, ConsultaMateriaisValidador>()
                .BuildServiceProvider(),
        };
        var contexto = new DefaultEndpointFilterInvocationContext(http, argumento, "outro argumento");

        return await new FiltroValidacao<ConsultaMateriais>().InvokeAsync(contexto, _ => ValueTask.FromResult<object?>(Seguiu));
    }
}
