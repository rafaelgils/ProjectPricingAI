import { useId } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { ErroApi } from '../api/cliente.ts';
import type { Usuario } from '../api/tipos.ts';
import Aviso from '../componentes/Aviso.tsx';
import { nomePapel, PAPEIS } from '../formatacao.ts';
import { resolverZod } from '../formularios/resolverZod.ts';
import { useSalvarUsuario } from './consultas.ts';

const TAMANHO_NOME = 100;
const TAMANHO_MINIMO_SENHA = 8;

const nome = (campo: string) =>
  z
    .string()
    .trim()
    .min(1, `Informe o ${campo}.`)
    .max(TAMANHO_NOME, `No máximo ${TAMANHO_NOME} caracteres.`);

/**
 * Mesmas regras da API; a API valida de novo e o Keycloak decide a duplicidade.
 * Nome de usuário e senha temporária só são informados no cadastro.
 */
const schemaPara = (novo: boolean) =>
  z.object({
    usuario: novo
      ? z
          .string()
          .trim()
          .regex(
            /^[a-zA-Z0-9._-]{3,50}$/,
            'Use de 3 a 50 letras, números, ponto, hífen ou sublinhado, sem espaços.',
          )
      : z.string(),
    nome: nome('nome'),
    sobrenome: nome('sobrenome'),
    email: z.string().trim().min(1, 'Informe o e-mail.').pipe(z.email('E-mail inválido.')),
    papel: z.enum(['admin', 'cliente-interno', 'cliente-externo'], { error: 'Escolha o papel.' }),
    senhaTemporaria: novo
      ? z
          .string()
          .min(
            TAMANHO_MINIMO_SENHA,
            `A senha temporária tem no mínimo ${TAMANHO_MINIMO_SENHA} caracteres.`,
          )
      : z.string(),
    ativo: z.boolean(),
  });

type Campos = z.infer<ReturnType<typeof schemaPara>>;
type NomeCampo = keyof Campos;
const CAMPOS: NomeCampo[] = [
  'usuario',
  'nome',
  'sobrenome',
  'email',
  'papel',
  'senhaTemporaria',
  'ativo',
];

interface Props {
  usuario?: Usuario;
  aoConcluir: () => void;
  aoCancelar: () => void;
}

/** Cadastro (com senha temporária) e alteração de usuário pelo Admin (RF10). */
function FormularioUsuario({ usuario, aoConcluir, aoCancelar }: Props) {
  const idTitulo = useId();
  const salvar = useSalvarUsuario();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<Campos>({
    resolver: resolverZod(schemaPara(!usuario)),
    defaultValues: {
      usuario: usuario?.usuario ?? '',
      nome: usuario?.nome ?? '',
      sobrenome: usuario?.sobrenome ?? '',
      email: usuario?.email ?? '',
      papel: usuario?.papel ?? 'cliente-interno',
      senhaTemporaria: '',
      ativo: usuario?.ativo ?? true,
    },
  });

  const enviar = handleSubmit(async ({ usuario: login, senhaTemporaria, ativo, ...dados }) => {
    try {
      if (usuario) {
        await salvar.mutateAsync({ id: usuario.id, dados: { ...dados, ativo } });
      } else {
        await salvar.mutateAsync({ dados: { ...dados, usuario: login, senhaTemporaria } });
      }
      aoConcluir();
    } catch (erro) {
      apontarErrosDaApi(erro);
    }
  });

  /** 400 traz erros por campo; 409 é usuário ou e-mail repetido. */
  const apontarErrosDaApi = (erro: unknown) => {
    if (!(erro instanceof ErroApi)) {
      return;
    }
    if (erro.codigo === 'USUARIO_DUPLICADO') {
      setError(usuario ? 'email' : 'usuario', { message: erro.message });
      return;
    }
    for (const [campo, mensagens] of Object.entries(erro.problema.errors ?? {})) {
      if ((CAMPOS as string[]).includes(campo)) {
        setError(campo as NomeCampo, { message: mensagens[0] });
      }
    }
  };

  const apontadoNosCampos = (erro: Error): boolean =>
    erro instanceof ErroApi &&
    (erro.codigo === 'USUARIO_DUPLICADO' || (erro.status === 400 && Boolean(erro.problema.errors)));

  const campo = (nomeCampo: NomeCampo) => ({
    id: `${idTitulo}-${nomeCampo}`,
    'aria-invalid': errors[nomeCampo] ? true : undefined,
    'aria-describedby': errors[nomeCampo] ? `${idTitulo}-${nomeCampo}-erro` : undefined,
  });
  const erro = (nomeCampo: NomeCampo) =>
    errors[nomeCampo] && (
      <span className="erro-campo" id={`${idTitulo}-${nomeCampo}-erro`}>
        {errors[nomeCampo]?.message}
      </span>
    );

  return (
    <form
      className="formulario"
      onSubmit={(e) => void enviar(e)}
      aria-labelledby={idTitulo}
      noValidate
    >
      <h2 id={idTitulo}>{usuario ? `Alterar ${usuario.usuario}` : 'Novo usuário'}</h2>

      {salvar.error && !apontadoNosCampos(salvar.error) && (
        <Aviso tipo="erro" titulo={salvar.error.message} />
      )}

      <div className="grade-campos">
        {!usuario && (
          <>
            <label htmlFor={`${idTitulo}-usuario`}>Nome de usuário</label>
            <input {...campo('usuario')} {...register('usuario')} autoComplete="off" />
            {erro('usuario')}
          </>
        )}

        <label htmlFor={`${idTitulo}-nome`}>Nome</label>
        <input {...campo('nome')} {...register('nome')} />
        {erro('nome')}

        <label htmlFor={`${idTitulo}-sobrenome`}>Sobrenome</label>
        <input {...campo('sobrenome')} {...register('sobrenome')} />
        {erro('sobrenome')}

        <label htmlFor={`${idTitulo}-email`}>E-mail</label>
        <input {...campo('email')} {...register('email')} type="email" />
        {erro('email')}

        <label htmlFor={`${idTitulo}-papel`}>Papel</label>
        <select {...campo('papel')} {...register('papel')}>
          {PAPEIS.map((papel) => (
            <option key={papel} value={papel}>
              {nomePapel(papel)}
            </option>
          ))}
        </select>
        {erro('papel')}

        {!usuario && (
          <>
            <label htmlFor={`${idTitulo}-senhaTemporaria`}>Senha temporária</label>
            <input
              {...campo('senhaTemporaria')}
              {...register('senhaTemporaria')}
              type="password"
              autoComplete="new-password"
            />
            {erro('senhaTemporaria')}
          </>
        )}

        {usuario && (
          <>
            <label htmlFor={`${idTitulo}-ativo`}>Ativo</label>
            <input {...campo('ativo')} {...register('ativo')} type="checkbox" />
          </>
        )}
      </div>

      {!usuario && <p className="dica">O usuário troca a senha temporária no primeiro acesso.</p>}

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

export default FormularioUsuario;
