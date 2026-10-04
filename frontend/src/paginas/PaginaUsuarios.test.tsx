import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { Pagina, Usuario } from '../api/tipos.ts';
import { perfilCom, renderizarApp, respostaJson, simularFetch } from '../teste/utilitarios.tsx';

const MARIA: Usuario = {
  id: 'u2',
  usuario: 'maria',
  nome: 'Maria',
  sobrenome: 'Silva',
  email: 'maria@exemplo.local',
  papel: 'cliente-interno',
  ativo: true,
  criadoEm: '2026-10-04T14:30:00-03:00',
};
const ANA: Usuario = { ...MARIA, id: 'kc-1', usuario: 'ana', nome: 'Ana', papel: 'admin' };
const pagina = (itens: Usuario[]): Pagina<Usuario> => ({
  itens,
  pagina: 1,
  tamanho: 20,
  total: itens.length,
});

describe('Gestão de usuários pelo Admin', () => {
  it('lista com papel e situação e não deixa o Admin se desativar', async () => {
    simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('admin'),
      'GET /api/v1/usuarios': () => respostaJson(pagina([ANA, MARIA])),
    });
    renderizarApp('/usuarios');

    const linhaMaria = (await screen.findByText('maria')).closest('tr') as HTMLElement;
    expect(within(linhaMaria).getByText('Cliente interno')).toBeInTheDocument();
    expect(within(linhaMaria).getByText('Ativo')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Desativar maria' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Desativar ana' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Usuários' })).toBeInTheDocument();
  });

  it('valida e cadastra com senha temporária', async () => {
    const chamadas = simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('admin'),
      'GET /api/v1/usuarios': () => respostaJson(pagina([])),
      'POST /api/v1/usuarios': () => respostaJson({ ...MARIA, id: 'u3', usuario: 'joao' }, 201),
    });
    renderizarApp('/usuarios');

    await userEvent.click(await screen.findByRole('button', { name: 'Novo usuário' }));
    const formulario = screen.getByRole('form', { name: 'Novo usuário' });
    await userEvent.type(within(formulario).getByLabelText('Nome de usuário'), 'joao silva');
    await userEvent.type(within(formulario).getByLabelText('Senha temporária'), '123');
    await userEvent.click(within(formulario).getByRole('button', { name: 'Salvar' }));

    expect(await within(formulario).findByText(/sem espaços/)).toBeInTheDocument();
    expect(within(formulario).getByText('Informe o nome.')).toBeInTheDocument();
    expect(within(formulario).getByText(/no mínimo 8 caracteres/)).toBeInTheDocument();
    expect(chamadas.some((c) => c.metodo === 'POST')).toBe(false);

    const usuario = within(formulario).getByLabelText('Nome de usuário');
    await userEvent.clear(usuario);
    await userEvent.type(usuario, 'joao');
    await userEvent.type(within(formulario).getByLabelText('Nome'), 'João');
    await userEvent.type(within(formulario).getByLabelText('Sobrenome'), 'Souza');
    await userEvent.type(within(formulario).getByLabelText('E-mail'), 'joao@exemplo.local');
    await userEvent.selectOptions(within(formulario).getByLabelText('Papel'), 'cliente-externo');
    await userEvent.type(within(formulario).getByLabelText('Senha temporária'), '45678');
    await userEvent.click(within(formulario).getByRole('button', { name: 'Salvar' }));

    await waitFor(() => expect(screen.queryByRole('form')).not.toBeInTheDocument());
    expect(chamadas.find((c) => c.metodo === 'POST')?.corpo).toEqual({
      usuario: 'joao',
      nome: 'João',
      sobrenome: 'Souza',
      email: 'joao@exemplo.local',
      papel: 'cliente-externo',
      senhaTemporaria: '12345678',
    });
  });

  it('mostra no campo a duplicidade apontada pela API', async () => {
    simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('admin'),
      'GET /api/v1/usuarios': () => respostaJson(pagina([MARIA])),
      'PUT /api/v1/usuarios/u2': () =>
        respostaJson(
          {
            status: 409,
            code: 'USUARIO_DUPLICADO',
            title: 'Já existe um usuário com esse nome de usuário ou e-mail.',
          },
          409,
        ),
    });
    renderizarApp('/usuarios');

    await userEvent.click(await screen.findByRole('button', { name: 'Alterar maria' }));
    const formulario = screen.getByRole('form', { name: 'Alterar maria' });
    expect(within(formulario).queryByLabelText('Senha temporária')).not.toBeInTheDocument();
    expect(within(formulario).getByLabelText('Ativo')).toBeChecked();
    await userEvent.click(within(formulario).getByRole('button', { name: 'Salvar' }));

    expect(await within(formulario).findByText(/Já existe um usuário/)).toBeInTheDocument();
    expect(within(formulario).getByLabelText('E-mail')).toHaveAttribute('aria-invalid', 'true');
  });

  it('desativa após confirmação', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const chamadas = simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('admin'),
      'GET /api/v1/usuarios': () => respostaJson(pagina([MARIA])),
      'DELETE /api/v1/usuarios/u2': () => new Response(null, { status: 204 }),
    });
    renderizarApp('/usuarios');

    await userEvent.click(await screen.findByRole('button', { name: 'Desativar maria' }));

    await waitFor(() => expect(chamadas.some((c) => c.metodo === 'DELETE')).toBe(true));
  });
});

describe('Gestão de usuários para clientes', () => {
  it('cliente-interno não vê o menu nem a página e nada é consultado', async () => {
    const chamadas = simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('cliente-interno'),
    });
    renderizarApp('/usuarios');

    expect(
      await screen.findByText('Você não tem acesso à gestão de usuários.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Usuários' })).not.toBeInTheDocument();
    expect(chamadas.filter((c) => c.caminho.startsWith('/api/v1/usuarios?'))).toHaveLength(0);
  });
});
