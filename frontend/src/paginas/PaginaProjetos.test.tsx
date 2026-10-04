import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { Pagina, ProjetoResumo } from '../api/tipos.ts';
import { perfilCom, renderizarApp, respostaJson, simularFetch } from '../teste/utilitarios.tsx';

const PROJETOS: Pagina<ProjetoResumo> = {
  itens: [
    {
      id: 'p1',
      descricao: 'Placa de trânsito 60x60',
      status: 'cotado',
      valorTotal: 105.9,
      moeda: 'BRL',
      criadoEm: '2026-10-04T14:30:00-03:00',
      alteradoEm: '2026-10-04T14:30:00-03:00',
    },
    {
      id: 'p2',
      descricao: 'Faixa refletiva',
      status: 'arquivado',
      valorTotal: null,
      moeda: 'BRL',
      criadoEm: '2026-10-03T10:00:00-03:00',
      alteradoEm: '2026-10-03T10:00:00-03:00',
    },
  ],
  pagina: 1,
  tamanho: 20,
  total: 2,
};

describe('Lista de projetos (RF07)', () => {
  it('mostra valor e status e exclui depois de confirmar', async () => {
    const chamadas = simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('cliente-interno'),
      'GET /api/v1/projetos': () => respostaJson(PROJETOS),
      'DELETE /api/v1/projetos/p1': () => new Response(null, { status: 204 }),
    });
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    renderizarApp('/projetos');

    expect(await screen.findByRole('link', { name: 'Placa de trânsito 60x60' })).toHaveAttribute(
      'href',
      '/projetos/p1',
    );
    expect(screen.getByText(/R\$\s*105,90/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Meus projetos' })).toBeInTheDocument();
    // Projeto arquivado não tem o botão de excluir.
    expect(
      screen.queryByRole('button', { name: 'Excluir o projeto Faixa refletiva' }),
    ).not.toBeInTheDocument();

    await userEvent.click(
      screen.getByRole('button', { name: 'Excluir o projeto Placa de trânsito 60x60' }),
    );

    await waitFor(() =>
      expect(
        chamadas.some((c) => c.metodo === 'DELETE' && c.caminho === '/api/v1/projetos/p1'),
      ).toBe(true),
    );
  });

  it('filtra pelo status pedido', async () => {
    const chamadas = simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('admin'),
      'GET /api/v1/projetos': () => respostaJson(PROJETOS),
    });
    renderizarApp('/projetos');

    expect(
      await screen.findByRole('heading', { name: 'Projetos de todos os clientes' }),
    ).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByLabelText('Status'), 'arquivado');

    await waitFor(() =>
      expect(chamadas.some((c) => c.caminho.includes('status=arquivado'))).toBe(true),
    );
  });
});
