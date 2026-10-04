import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { Projeto } from '../api/tipos.ts';
import {
  perfilCom,
  renderizarApp,
  respostaJson,
  respostaSse,
  simularFetch,
} from '../teste/utilitarios.tsx';

const ID = 'p1';
const PROJETO_COTADO: Projeto = {
  id: ID,
  descricao: 'Placa de trânsito 60x60',
  status: 'cotado',
  itens: [
    {
      materialId: 'm1',
      nome: 'Chapa de aço galvanizado',
      quantidade: 0.36,
      unidade: 'm2',
      precoUnitario: 120,
      subtotal: 43.2,
    },
    {
      materialId: 'm2',
      nome: 'Película refletiva',
      quantidade: 0.36,
      unidade: 'm2',
      precoUnitario: 95,
      subtotal: 34.2,
    },
    {
      materialId: 'm3',
      nome: 'Cabeçote de metal',
      quantidade: 1,
      unidade: 'un',
      precoUnitario: 28.5,
      subtotal: 28.5,
    },
  ],
  valorTotal: 105.9,
  moeda: 'BRL',
  criadoEm: '2026-10-04T14:30:00-03:00',
  alteradoEm: '2026-10-04T14:30:00-03:00',
};
const PROJETO_RASCUNHO: Projeto = {
  ...PROJETO_COTADO,
  status: 'rascunho',
  itens: [],
  valorTotal: null,
};

describe('Nova cotação', () => {
  it('envia a descrição, mostra a resposta em streaming e abre o projeto cotado', async () => {
    const chamadas = simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('cliente-externo'),
      'POST /api/v1/projetos': () =>
        respostaSse(
          [
            { evento: 'delta', dados: { texto: 'Analisando a descrição do projeto...' } },
            {
              evento: 'delta',
              dados: { texto: 'Para esse projeto o valor estimado é R$ 105,90.' },
            },
            { evento: 'cotacao', dados: PROJETO_COTADO },
            { evento: 'fim', dados: { projetoId: ID, status: 'cotado' } },
          ],
          201,
        ),
      'GET /api/v1/projetos/p1': () => respostaJson(PROJETO_COTADO),
      'GET /api/v1/projetos/p1/mensagens': () =>
        respostaJson([
          {
            papel: 'usuario',
            conteudo: 'Placa de trânsito 60x60',
            enviadaEm: '2026-10-04T14:30:00-03:00',
          },
          {
            papel: 'assistente',
            conteudo: 'Para esse projeto o valor estimado é R$ 105,90.',
            enviadaEm: '2026-10-04T14:30:01-03:00',
          },
        ]),
    });
    const { roteador } = renderizarApp('/projetos/novo');

    await userEvent.type(
      await screen.findByLabelText('Descreva o projeto'),
      'Placa de trânsito 60x60',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Cotar' }));

    const tabela = await screen.findByRole('table', { name: 'Itens da cotação' });
    expect(within(tabela).getByText('Película refletiva')).toBeInTheDocument();
    expect(within(tabela).getAllByText(/0,36\s*m²/)).toHaveLength(2);
    expect(within(tabela).getByText(/R\$\s*105,90/)).toBeInTheDocument();
    // Depois do fim, a página do projeto recarrega o histórico gravado pelo agente.
    expect(
      await within(screen.getByRole('log')).findByText(/valor estimado é R\$ 105,90/),
    ).toBeInTheDocument();
    await waitFor(() => expect(roteador.state.location.pathname).toBe('/projetos/p1'));
    expect(chamadas.find((c) => c.metodo === 'POST')?.corpo).toEqual({
      descricao: 'Placa de trânsito 60x60',
    });
  });

  it('lista os itens que não estão no catálogo, sem calcular valor (RN03)', async () => {
    simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('cliente-interno'),
      'POST /api/v1/projetos': () =>
        respostaSse([
          { evento: 'delta', dados: { texto: 'Analisando a descrição do projeto...' } },
          {
            evento: 'erro',
            dados: {
              status: 422,
              code: 'ITENS_NAO_ENCONTRADOS',
              title: 'Itens não encontrados no catálogo',
              itensNaoEncontrados: ['cabeçote de metal'],
            },
          },
          { evento: 'fim', dados: { projetoId: ID, status: 'rascunho' } },
        ]),
      'GET /api/v1/projetos/p1': () => respostaJson(PROJETO_RASCUNHO),
      'GET /api/v1/projetos/p1/mensagens': () => respostaJson([]),
    });
    renderizarApp('/projetos/novo');

    await userEvent.type(
      await screen.findByLabelText('Descreva o projeto'),
      'Placa com cabeçote de metal',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Cotar' }));

    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent('Alguns itens não estão no catálogo');
    expect(within(alerta).getByText('cabeçote de metal')).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: 'Itens da cotação' })).not.toBeInTheDocument();
  });
});

describe('Confirmação de material (RN09)', () => {
  it('mostra as sugestões e envia a confirmação escolhida como mensagem', async () => {
    const chamadas = simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('cliente-interno'),
      'GET /api/v1/projetos/p1': () => respostaJson(PROJETO_RASCUNHO),
      'GET /api/v1/projetos/p1/mensagens': () => respostaJson([]),
      'POST /api/v1/projetos/p1/mensagens': [
        () =>
          respostaSse([
            {
              evento: 'erro',
              dados: {
                status: 422,
                code: 'ESCLARECIMENTO_NECESSARIO',
                pergunta: 'Antes de calcular, confirme:',
                sugestoes: [
                  {
                    termo: 'película reflexiva',
                    materialId: 'm2',
                    nome: 'Película refletiva',
                    origem: 'similaridade',
                    similaridade: 0.94,
                  },
                  {
                    termo: 'cantoneira',
                    materialId: 'm9',
                    nome: 'Suporte em L de aço',
                    origem: 'agente',
                    similaridade: null,
                  },
                ],
              },
            },
            { evento: 'fim', dados: { projetoId: ID, status: 'rascunho' } },
          ]),
        () =>
          respostaSse([
            { evento: 'cotacao', dados: PROJETO_COTADO },
            { evento: 'fim', dados: { projetoId: ID, status: 'cotado' } },
          ]),
      ],
    });
    renderizarApp('/projetos/p1');

    await userEvent.type(
      await screen.findByLabelText(/Ajuste a cotação/),
      'Película reflexiva e cantoneira',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Enviar' }));

    const sugestoes = await screen.findByRole('list', { name: 'Sugestões de material' });
    expect(screen.getByText('(94% parecido)')).toBeInTheDocument();
    expect(screen.getByText('(sugerido pelo assistente)')).toBeInTheDocument();
    await userEvent.click(within(sugestoes).getAllByRole('button', { name: 'Confirmar' })[0]);

    await waitFor(() =>
      expect(chamadas.filter((c) => c.metodo === 'POST').at(-1)?.corpo).toEqual({
        conteudo: 'Sim, pode usar "Película refletiva" para "película reflexiva".',
      }),
    );
  });
});

describe('Projeto arquivado (RN11)', () => {
  it('é somente leitura: mostra a cotação sem o campo de mensagem', async () => {
    simularFetch({
      'GET /api/v1/usuarios/me': perfilCom('cliente-interno'),
      'GET /api/v1/projetos/p1': () => respostaJson({ ...PROJETO_COTADO, status: 'arquivado' }),
      'GET /api/v1/projetos/p1/mensagens': () => respostaJson([]),
    });
    renderizarApp('/projetos/p1');

    expect(
      await screen.findByText('Este projeto está arquivado e só pode ser consultado.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('table', { name: 'Itens da cotação' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
  });
});
