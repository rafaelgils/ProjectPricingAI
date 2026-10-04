import { describe, expect, it } from 'vitest';
import { respostaJson, simularFetch, URL_API } from '../teste/utilitarios.tsx';
import { ClienteApi, ErroApi } from './cliente.ts';
import { lerEventosSse } from './sse.ts';

function stream(...pedacos: Uint8Array[]): ReadableStream<Uint8Array> {
  return new ReadableStream({
    start(controle) {
      pedacos.forEach((p) => controle.enqueue(p));
      controle.close();
    },
  });
}

async function coletar(corpo: ReadableStream<Uint8Array>) {
  const eventos = [];
  for await (const evento of lerEventosSse(corpo)) {
    eventos.push(evento);
  }
  return eventos;
}

describe('lerEventosSse', () => {
  it('junta eventos que chegam partidos, inclusive no meio de um caractere acentuado', async () => {
    const bytes = new TextEncoder().encode(
      'event: delta\ndata: {"texto":"Película"}\n\nevent: fim\r\ndata: {"projetoId":"p1","status":"cotado"}\r\n\r\n',
    );
    const corte = bytes.indexOf(0xc3) + 1; // entre os dois bytes do "í"

    const eventos = await coletar(stream(bytes.slice(0, corte), bytes.slice(corte)));

    expect(eventos).toEqual([
      { tipo: 'delta', dados: { texto: 'Película' } },
      { tipo: 'fim', dados: { projetoId: 'p1', status: 'cotado' } },
    ]);
  });

  it('ignora eventos desconhecidos e blocos sem dados', async () => {
    const eventos = await coletar(
      stream(new TextEncoder().encode('event: ping\ndata: {}\n\nevent: delta\n\n: comentario\n\n')),
    );

    expect(eventos).toEqual([]);
  });
});

describe('ClienteApi', () => {
  const api = new ClienteApi(URL_API, () => 'meu-token');

  it('envia o token e o corpo em JSON', async () => {
    const chamadas = simularFetch({
      'POST /api/v1/materiais': () => respostaJson({ id: 'm1' }, 201),
    });

    const material = await api.requisitar<{ id: string }>('/api/v1/materiais', {
      metodo: 'POST',
      corpo: { nome: 'Chapa' },
    });

    expect(material.id).toBe('m1');
    expect(chamadas[0].cabecalhos.Authorization).toBe('Bearer meu-token');
    expect(chamadas[0].corpo).toEqual({ nome: 'Chapa' });
  });

  it('transforma Problem Details em ErroApi com o code', async () => {
    simularFetch({
      'GET /api/v1/projetos/x': () =>
        respostaJson(
          { status: 404, title: 'Projeto "x" não encontrado.', code: 'RECURSO_NAO_ENCONTRADO' },
          404,
        ),
    });

    const erro = await api.requisitar('/api/v1/projetos/x').catch((e: unknown) => e);

    expect(erro).toBeInstanceOf(ErroApi);
    expect((erro as ErroApi).codigo).toBe('RECURSO_NAO_ENCONTRADO');
    expect((erro as ErroApi).message).toBe('Projeto "x" não encontrado.');
  });

  it('traduz o 401 do Kong, que não usa Problem Details', async () => {
    simularFetch({
      'GET /api/v1/usuarios/me': () => respostaJson({ message: 'Unauthorized' }, 401),
    });

    const erro = (await api.requisitar('/api/v1/usuarios/me').catch((e: unknown) => e)) as ErroApi;

    expect(erro.status).toBe(401);
    expect(erro.message).toBe('Sua sessão expirou. Entre novamente.');
  });

  it('exclusão aceita 204 sem corpo', async () => {
    simularFetch({ 'DELETE /api/v1/projetos/p1': () => new Response(null, { status: 204 }) });

    await expect(api.excluir('/api/v1/projetos/p1')).resolves.toBeUndefined();
  });
});
