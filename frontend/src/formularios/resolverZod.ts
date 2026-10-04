import type { FieldErrors, FieldValues, Resolver } from 'react-hook-form';
import type { z } from 'zod';

/**
 * Liga um schema do zod ao react-hook-form, apontando cada erro no seu campo.
 * Feito aqui para não depender de outra biblioteca fora do tech-stack.md.
 */
export function resolverZod<T extends FieldValues>(schema: z.ZodType<T, T>): Resolver<T> {
  return (valores) => {
    const resultado = schema.safeParse(valores);
    if (resultado.success) {
      return { values: resultado.data, errors: {} };
    }

    const erros: Record<string, { type: string; message: string }> = {};
    for (const problema of resultado.error.issues) {
      const campo = String(problema.path[0] ?? 'root');
      erros[campo] ??= { type: problema.code, message: problema.message };
    }
    return { values: {}, errors: erros as FieldErrors<T> };
  };
}
