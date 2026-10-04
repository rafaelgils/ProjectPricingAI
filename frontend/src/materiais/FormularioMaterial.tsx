import { useId } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { ErroApi } from '../api/cliente.ts';
import type { DadosMaterial, Material } from '../api/tipos.ts';
import Aviso from '../componentes/Aviso.tsx';
import { resolverZod } from '../formularios/resolverZod.ts';
import { useSalvarMaterial } from './consultas.ts';

const TAMANHO_NOME = 150;

/** Mesmas regras da API (RN10); a API valida de novo e decide a duplicidade. */
const schema = z.object({
  nome: z
    .string()
    .trim()
    .min(1, 'Informe o nome.')
    .max(TAMANHO_NOME, `No máximo ${TAMANHO_NOME} caracteres.`),
  sinonimos: z.string(),
  tipo: z.enum(['material', 'servico'], { error: 'Escolha o tipo.' }),
  categoria: z.string().trim().min(1, 'Informe a categoria.').max(100, 'No máximo 100 caracteres.'),
  unidade: z.enum(['m', 'm2', 'un', 'L', 'h'], { error: 'Escolha a unidade.' }),
  precoUnitario: z
    .number({ error: 'Informe o preço unitário.' })
    .positive('O preço precisa ser maior que zero.')
    .refine(
      (preco) => Math.abs(preco * 100 - Math.round(preco * 100)) < 1e-6,
      'Use no máximo 2 casas decimais.',
    ),
  fornecedor: z
    .string()
    .trim()
    .min(1, 'Informe o fornecedor.')
    .max(TAMANHO_NOME, `No máximo ${TAMANHO_NOME} caracteres.`),
  status: z.enum(['ativo', 'inativo']),
});

type Campos = z.infer<typeof schema>;

/** Erros que já aparecem junto do campo: validação (400) e nome ou sinônimo repetido (409). */
const apontadoNosCampos = (erro: Error): boolean =>
  erro instanceof ErroApi &&
  (erro.codigo === 'MATERIAL_DUPLICADO' || (erro.status === 400 && Boolean(erro.problema.errors)));

/** Sinônimos são digitados separados por vírgula. */
const lerSinonimos = (texto: string): string[] =>
  texto
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);

interface Props {
  material?: Material;
  aoConcluir: () => void;
  aoCancelar: () => void;
}

/** Cadastro e alteração de material ou serviço pelo Admin (RF02). */
function FormularioMaterial({ material, aoConcluir, aoCancelar }: Props) {
  const idTitulo = useId();
  const salvar = useSalvarMaterial();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<Campos>({
    resolver: resolverZod(schema),
    defaultValues: {
      nome: material?.nome ?? '',
      sinonimos: material?.sinonimos.join(', ') ?? '',
      tipo: material?.tipo ?? 'material',
      categoria: material?.categoria ?? '',
      unidade: material?.unidade ?? 'un',
      precoUnitario: material?.precoUnitario,
      fornecedor: material?.fornecedor ?? '',
      status: material?.status ?? 'ativo',
    },
  });

  const enviar = handleSubmit(async ({ sinonimos, status, ...campos }) => {
    const dados: DadosMaterial = { ...campos, sinonimos: lerSinonimos(sinonimos) };
    try {
      await salvar.mutateAsync({ id: material?.id, dados, status: material ? status : undefined });
      aoConcluir();
    } catch (erro) {
      apontarErrosDaApi(erro);
    }
  });

  /** 400 traz erros por campo; 409 é nome ou sinônimo repetido (RN10). */
  const apontarErrosDaApi = (erro: unknown) => {
    if (!(erro instanceof ErroApi)) {
      return;
    }
    if (erro.codigo === 'MATERIAL_DUPLICADO') {
      setError('nome', { message: erro.message });
      return;
    }
    for (const [campo, mensagens] of Object.entries(erro.problema.errors ?? {})) {
      if (campo in schema.shape) {
        setError(campo as keyof Campos, { message: mensagens[0] });
      }
    }
  };

  const campo = (nome: keyof Campos) => ({
    id: `${idTitulo}-${nome}`,
    'aria-invalid': errors[nome] ? true : undefined,
    'aria-describedby': errors[nome] ? `${idTitulo}-${nome}-erro` : undefined,
  });
  const erro = (nome: keyof Campos) =>
    errors[nome] && (
      <span className="erro-campo" id={`${idTitulo}-${nome}-erro`}>
        {errors[nome]?.message}
      </span>
    );

  return (
    <form
      className="formulario"
      onSubmit={(e) => void enviar(e)}
      aria-labelledby={idTitulo}
      noValidate
    >
      <h2 id={idTitulo}>{material ? `Alterar ${material.nome}` : 'Novo material ou serviço'}</h2>

      {salvar.error && !apontadoNosCampos(salvar.error) && (
        <Aviso tipo="erro" titulo={salvar.error.message} />
      )}

      <div className="grade-campos">
        <label htmlFor={`${idTitulo}-nome`}>Nome</label>
        <input {...campo('nome')} {...register('nome')} />
        {erro('nome')}

        <label htmlFor={`${idTitulo}-sinonimos`}>Sinônimos (separados por vírgula)</label>
        <input {...campo('sinonimos')} {...register('sinonimos')} placeholder="Ex.: placa, chapa" />
        {erro('sinonimos')}

        <label htmlFor={`${idTitulo}-tipo`}>Tipo</label>
        <select {...campo('tipo')} {...register('tipo')}>
          <option value="material">Material</option>
          <option value="servico">Serviço</option>
        </select>
        {erro('tipo')}

        <label htmlFor={`${idTitulo}-categoria`}>Categoria</label>
        <input {...campo('categoria')} {...register('categoria')} />
        {erro('categoria')}

        <label htmlFor={`${idTitulo}-unidade`}>Unidade</label>
        <select {...campo('unidade')} {...register('unidade')}>
          <option value="un">Unidade (un)</option>
          <option value="m">Metro (m)</option>
          <option value="m2">Metro quadrado (m²)</option>
          <option value="L">Litro (L)</option>
          <option value="h">Hora (h)</option>
        </select>
        {erro('unidade')}

        <label htmlFor={`${idTitulo}-precoUnitario`}>Preço unitário (R$)</label>
        <input
          {...campo('precoUnitario')}
          {...register('precoUnitario', { valueAsNumber: true })}
          type="number"
          step="0.01"
          min="0.01"
          inputMode="decimal"
        />
        {erro('precoUnitario')}

        <label htmlFor={`${idTitulo}-fornecedor`}>Fornecedor</label>
        <input {...campo('fornecedor')} {...register('fornecedor')} />
        {erro('fornecedor')}

        {material && (
          <>
            <label htmlFor={`${idTitulo}-status`}>Status</label>
            <select {...campo('status')} {...register('status')}>
              <option value="ativo">Ativo</option>
              <option value="inativo">Inativo</option>
            </select>
          </>
        )}
      </div>

      <div className="acoes">
        <button type="submit" disabled={salvar.isPending}>
          {salvar.isPending ? 'Salvando...' : 'Salvar'}
        </button>
        <button type="button" className="botao-secundario" onClick={aoCancelar}>
          Cancelar
        </button>
      </div>
    </form>
  );
}

export default FormularioMaterial;
