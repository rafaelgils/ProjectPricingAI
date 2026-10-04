import { lerEventosSse } from './sse.ts';
import type { EventoConversa, Problema } from './tipos.ts';

/** Erro da API já interpretado a partir do Problem Details (standards.md §5). */
export class ErroApi extends Error {
  readonly status: number;
  readonly problema: Problema;

  constructor(status: number, problema: Problema) {
    super(problema.title ?? `Erro ${status}`);
    this.name = 'ErroApi';
    this.status = status;
    this.problema = problema;
  }

  get codigo(): string | undefined {
    return this.problema.code;
  }
}

export interface OpcoesRequisicao {
  metodo?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  corpo?: unknown;
  sinal?: AbortSignal;
}

/**
 * Cliente HTTP único (standards.md §4): injeta o token de acesso e interpreta Problem Details.
 * O token é lido a cada chamada, para usar sempre o mais recente após a renovação silenciosa.
 */
export class ClienteApi {
  private readonly urlBase: string;
  private readonly obterToken: () => string | undefined;

  constructor(urlBase: string, obterToken: () => string | undefined) {
    this.urlBase = urlBase;
    this.obterToken = obterToken;
  }

  async requisitar<T>(caminho: string, opcoes: OpcoesRequisicao = {}): Promise<T> {
    const resposta = await this.enviar(caminho, opcoes, 'application/json');
    if (resposta.status === 204) {
      return undefined as T;
    }
    return (await resposta.json()) as T;
  }

  async excluir(caminho: string): Promise<void> {
    await this.enviar(caminho, { metodo: 'DELETE' }, 'application/json');
  }

  /** POST que responde em SSE: devolve os eventos à medida que chegam. */
  async *conversar(
    caminho: string,
    corpo: unknown,
    sinal?: AbortSignal,
  ): AsyncGenerator<EventoConversa> {
    const resposta = await this.enviar(
      caminho,
      { metodo: 'POST', corpo, sinal },
      'text/event-stream',
    );
    if (!resposta.body) {
      throw new ErroApi(resposta.status, { title: 'Resposta sem conteúdo.' });
    }
    yield* lerEventosSse(resposta.body);
  }

  private async enviar(
    caminho: string,
    opcoes: OpcoesRequisicao,
    aceita: string,
  ): Promise<Response> {
    const cabecalhos: Record<string, string> = { Accept: aceita };
    const token = this.obterToken();
    if (token) {
      cabecalhos.Authorization = `Bearer ${token}`;
    }
    if (opcoes.corpo !== undefined) {
      cabecalhos['Content-Type'] = 'application/json';
    }

    const resposta = await fetch(`${this.urlBase}${caminho}`, {
      method: opcoes.metodo ?? 'GET',
      headers: cabecalhos,
      body: opcoes.corpo === undefined ? undefined : JSON.stringify(opcoes.corpo),
      signal: opcoes.sinal,
    });

    if (!resposta.ok) {
      throw new ErroApi(resposta.status, await lerProblema(resposta));
    }
    return resposta;
  }
}

async function lerProblema(resposta: Response): Promise<Problema> {
  try {
    const problema = (await resposta.json()) as Problema & { message?: string };
    // O Kong responde 401 no formato dele: {"message":"Unauthorized"}.
    return problema.title
      ? problema
      : { status: resposta.status, title: mensagemPadrao(resposta.status) };
  } catch {
    return { status: resposta.status, title: mensagemPadrao(resposta.status) };
  }
}

function mensagemPadrao(status: number): string {
  if (status === 401) return 'Sua sessão expirou. Entre novamente.';
  if (status === 403) return 'Você não tem permissão para esta ação.';
  if (status === 404) return 'Não encontrado.';
  return 'Não foi possível concluir a operação. Tente novamente.';
}
