import type { EventoConversa } from './tipos.ts';

const TIPOS_CONHECIDOS = new Set(['delta', 'cotacao', 'erro', 'fim']);

/**
 * Lê o text/event-stream da API (plano, P6: fetch com leitura de stream, porque o EventSource
 * não envia o cabeçalho Authorization). Cada evento tem `event:` e `data:` em JSON.
 */
export async function* lerEventosSse(
  corpo: ReadableStream<Uint8Array>,
): AsyncGenerator<EventoConversa> {
  const leitor = corpo.getReader();
  const decodificador = new TextDecoder();
  let pendente = '';

  try {
    for (;;) {
      const { value, done } = await leitor.read();
      if (done) {
        break;
      }

      // stream: true guarda os bytes de um caractere acentuado que chegou pela metade.
      pendente += decodificador.decode(value, { stream: true }).replace(/\r\n/g, '\n');
      let fimDoBloco = pendente.indexOf('\n\n');
      while (fimDoBloco >= 0) {
        const evento = interpretarBloco(pendente.slice(0, fimDoBloco));
        pendente = pendente.slice(fimDoBloco + 2);
        if (evento) {
          yield evento;
        }
        fimDoBloco = pendente.indexOf('\n\n');
      }
    }
  } finally {
    leitor.releaseLock();
  }
}

function interpretarBloco(bloco: string): EventoConversa | null {
  let tipo = '';
  const dados: string[] = [];
  for (const linha of bloco.split('\n')) {
    if (linha.startsWith('event:')) {
      tipo = linha.slice('event:'.length).trim();
    } else if (linha.startsWith('data:')) {
      dados.push(linha.slice('data:'.length).trimStart());
    }
  }

  if (!TIPOS_CONHECIDOS.has(tipo) || dados.length === 0) {
    return null;
  }
  return { tipo, dados: JSON.parse(dados.join('\n')) } as EventoConversa;
}
