using System.Globalization;
using FluentValidation;

namespace ProjectPricing.Api.Validacao;

/// <summary>
/// Valida o argumento <typeparamref name="T"/> do endpoint antes do handler (standards.md §3).
/// Erros saem como 400 VALIDACAO, com as mensagens agrupadas por campo.
/// </summary>
public sealed class FiltroValidacao<T> : IEndpointFilter
    where T : class
{
    public const string CampoCorpo = "corpo";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argumento = context.Arguments.OfType<T>().FirstOrDefault();
        if (argumento is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [CampoCorpo] = ["Informe os dados da requisição."],
            });
        }

        var validador = context.HttpContext.RequestServices.GetRequiredService<IValidator<T>>();
        var resultado = await validador.ValidateAsync(argumento, context.HttpContext.RequestAborted);
        if (resultado.IsValid)
        {
            return await next(context);
        }

        return TypedResults.ValidationProblem(resultado.ToDictionary());
    }
}

public static class ConfiguracaoValidacao
{
    /// <summary>Chaves dos erros em camelCase, iguais aos campos do JSON (ex.: "precoUnitario").</summary>
    public static void Aplicar()
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
        ValidatorOptions.Global.PropertyNameResolver = (_, membro, _) =>
            membro is null ? null : char.ToLowerInvariant(membro.Name[0]) + membro.Name[1..];
    }

    public static RouteHandlerBuilder Validar<T>(this RouteHandlerBuilder endpoint)
        where T : class
    {
        return endpoint
            .AddEndpointFilter<FiltroValidacao<T>>()
            .ProducesValidationProblem();
    }
}
