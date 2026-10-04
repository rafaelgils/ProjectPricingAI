import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { vi } from 'vitest';
import { ClienteApi } from '../api/cliente.ts';
import type { Papel } from '../api/tipos.ts';
import { ContextoApi } from '../auth/contextoApi.ts';
import { rotas } from '../rotas.tsx';

export const URL_API = 'http://api.teste';

type Manipulador = (init: RequestInit) => Response | Promise<Response>;

export interface ChamadaFetch {
  metodo: string;
  caminho: string;
  corpo: unknown;
  cabecalhos: Record<string, string>;
}

/**
 * fetch simulado por rota: chave "MÉTODO /caminho" (sem a query string). Uma lista de manipuladores
 * responde em sequência, para simular chamadas repetidas à mesma rota.
 */
export function simularFetch(rotasApi: Record<string, Manipulador | Manipulador[]>) {
  const chamadas: ChamadaFetch[] = [];
  const contagem = new Map<string, number>();

  const fetchFalso = vi.fn(async (entrada: RequestInfo | URL, init: RequestInit = {}) => {
    const url = new URL(String(entrada));
    const metodo = init.method ?? 'GET';
    const chave = `${metodo} ${url.pathname}`;
    chamadas.push({
      metodo,
      caminho: url.pathname + url.search,
      corpo: init.body ? JSON.parse(String(init.body)) : undefined,
      cabecalhos: (init.headers ?? {}) as Record<string, string>,
    });

    const manipulador = rotasApi[chave];
    if (!manipulador) {
      return respostaJson({ title: `Rota não simulada: ${chave}` }, 404);
    }
    const lista = Array.isArray(manipulador) ? manipulador : [manipulador];
    const vez = contagem.get(chave) ?? 0;
    contagem.set(chave, vez + 1);
    return lista[Math.min(vez, lista.length - 1)](init);
  });

  vi.stubGlobal('fetch', fetchFalso);
  return chamadas;
}

export function respostaJson(corpo: unknown, status = 200): Response {
  return new Response(JSON.stringify(corpo), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

/** Resposta em text/event-stream, enviada em pedaços para exercitar a leitura incremental. */
export function respostaSse(eventos: { evento: string; dados: unknown }[], status = 200): Response {
  const texto = eventos
    .map((e) => `event: ${e.evento}\ndata: ${JSON.stringify(e.dados)}\n\n`)
    .join('');
  const bytes = new TextEncoder().encode(texto);
  const meio = Math.floor(bytes.length / 2);
  const corpo = new ReadableStream<Uint8Array>({
    start(controle) {
      controle.enqueue(bytes.slice(0, meio));
      controle.enqueue(bytes.slice(meio));
      controle.close();
    },
  });
  return new Response(corpo, { status, headers: { 'Content-Type': 'text/event-stream' } });
}

export function perfilCom(papel: Papel) {
  return () =>
    respostaJson({
      keycloakId: 'kc-1',
      nome: 'Ana Teste',
      email: 'ana@exemplo.local',
      papeis: [papel],
    });
}

/** Renderiza o app real (rotas, react-query e cliente da API) a partir de uma URL; só o login é simulado. */
export function renderizarApp(caminho: string) {
  const clienteConsultas = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const api = new ClienteApi(URL_API, () => 'token-de-teste');
  const roteador = createMemoryRouter(rotas, { initialEntries: [caminho] });

  render(
    <QueryClientProvider client={clienteConsultas}>
      <ContextoApi.Provider value={api}>
        <RouterProvider router={roteador} />
      </ContextoApi.Provider>
    </QueryClientProvider>,
  );
  return { roteador };
}
