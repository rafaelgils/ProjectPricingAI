import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { Material, Pagina } from '../api/tipos.ts';
import { perfilCom, renderizarApp, respostaJson, simularFetch } from '../teste/utilitarios.tsx';

const PELICULA: Material = {
  id: 'm2',
  nome: 'Película refletiva',
  sinonimos: ['tinta reflexiva'],
  tipo: 'material',
  categoria: 'Sinalização',
  unidade: 'm2',
  precoUnitario: 95,
  moeda: 'BRL',
  fornecedor: 'Refletivos SA',
  status: 'ativo',
  atualizadoEm: '2026-10-04T14:30:00-03:00',
};
const pagina = (itens: Material[]): Pagina<Material> => ({
  itens,
  pagina: 1,
  tamanho: 20,
  total: itens.length,
});

describe('Catálogo para o Admin', () => {
  it('valida o formulário antes de enviar e cadastra com sinônimos e preço', async () => {
    const chamadas = simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('admin'),
      'GET /api/v1/materiais': () => respostaJson(pagina([PELICULA])),
      'POST /api/v1/materiais': () => respostaJson({ ...PELICULA, id: 'm1' }, 201),
    });
    renderizarApp('/catalogo');

    expect(await screen.findByText('Película refletiva')).toBeInTheDocument();
    expect(screen.getByText(/R\$\s*95,00/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Novo material' }));
    const formulario = screen.getByRole('form', { name: 'Novo material ou serviço' });
    await userEvent.click(within(formulario).getByRole('button', { name: 'Salvar' }));

    expect(await within(formulario).findByText('Informe o nome.')).toBeInTheDocument();
    expect(within(formulario).getByText('Informe o preço unitário.')).toBeInTheDocument();
    expect(within(formulario).getByLabelText('Nome')).toHaveAttribute('aria-invalid', 'true');
    expect(chamadas.some((c) => c.metodo === 'POST')).toBe(false);

    await userEvent.type(within(formulario).getByLabelText('Nome'), 'Chapa de aço galvanizado');
    await userEvent.type(within(formulario).getByLabelText(/Sinônimos/), 'placa, chapa');
    await userEvent.selectOptions(within(formulario).getByLabelText('Unidade'), 'm2');
    await userEvent.type(within(formulario).getByLabelText('Categoria'), 'Sinalização');
    await userEvent.type(within(formulario).getByLabelText('Preço unitário (R$)'), '120.5');
    await userEvent.type(within(formulario).getByLabelText('Fornecedor'), 'Aço SA');
    await userEvent.click(within(formulario).getByRole('button', { name: 'Salvar' }));

    await waitFor(() => expect(screen.queryByRole('form')).not.toBeInTheDocument());
    expect(chamadas.find((c) => c.metodo === 'POST')?.corpo).toEqual({
      nome: 'Chapa de aço galvanizado',
      sinonimos: ['placa', 'chapa'],
      tipo: 'material',
      categoria: 'Sinalização',
      unidade: 'm2',
      precoUnitario: 120.5,
      fornecedor: 'Aço SA',
    });
  });

  it('recusa preço com mais de duas casas decimais', async () => {
    simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('admin'),
      'GET /api/v1/materiais': () => respostaJson(pagina([])),
    });
    renderizarApp('/catalogo');

    await userEvent.click(await screen.findByRole('button', { name: 'Novo material' }));
    await userEvent.type(screen.getByLabelText('Preço unitário (R$)'), '1.234');
    await userEvent.click(screen.getByRole('button', { name: 'Salvar' }));

    expect(await screen.findByText('Use no máximo 2 casas decimais.')).toBeInTheDocument();
  });

  it('mostra no campo nome a duplicidade apontada pela API (RN10)', async () => {
    simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('admin'),
      'GET /api/v1/materiais': () => respostaJson(pagina([PELICULA])),
      'PUT /api/v1/materiais/m2': () =>
        respostaJson(
          {
            status: 409,
            code: 'MATERIAL_DUPLICADO',
            title: 'Já existe um material com o nome ou sinônimo "placa".',
          },
          409,
        ),
    });
    renderizarApp('/catalogo');

    await userEvent.click(
      await screen.findByRole('button', { name: 'Alterar Película refletiva' }),
    );
    const formulario = screen.getByRole('form', { name: 'Alterar Película refletiva' });
    expect(within(formulario).getByLabelText('Status')).toHaveValue('ativo');
    await userEvent.click(within(formulario).getByRole('button', { name: 'Salvar' }));

    expect(await within(formulario).findByText(/nome ou sinônimo "placa"/)).toBeInTheDocument();
  });
});

describe('Catálogo para clientes', () => {
  it('cliente-interno consulta sem botões de cadastro e sem filtro de status', async () => {
    simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('cliente-interno'),
      'GET /api/v1/materiais': () => respostaJson(pagina([PELICULA])),
    });
    renderizarApp('/catalogo');

    expect(await screen.findByText('Película refletiva')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Novo material' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Alterar/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Status')).not.toBeInTheDocument();
  });

  it('cliente-externo não tem o catálogo no menu nem na página', async () => {
    simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('cliente-externo'),
      'GET /api/v1/materiais': () =>
        respostaJson({ status: 403, code: 'ACESSO_NEGADO', title: 'Acesso negado.' }, 403),
    });
    renderizarApp('/catalogo');

    expect(await screen.findByText('Você não tem acesso ao catálogo.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Catálogo' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Projetos' })).toBeInTheDocument();
  });
});
