import { useCallback, useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ErroApi } from '../api/cliente.ts';
import type { Problema, StatusProjeto } from '../api/tipos.ts';
import { useApi } from '../auth/contextoApi.ts';
import { chavesProjetos } from './consultas.ts';

/** Mensagens desta rodada, mostradas enquanto o histórico do servidor não é recarregado. */
export interface MensagemAoVivo {
  papel: 'usuario' | 'assistente';
  conteudo: string;
}

export interface ResultadoRodada {
  projetoId: string;
  status: StatusProjeto;
  /** Erro da rodada, para continuar visível se a página mudar (ex.: projeto recém-criado). */
  problema: Problema | null;
}

interface Opcoes {
  /** Projeto existente (refinamento); sem ele, a primeira mensagem cria o projeto. */
  projetoId?: string;
  aoTerminar?: (resultado: ResultadoRodada) => void;
  /** Erro herdado da rodada anterior (ex.: a que criou o projeto). */
  problemaInicial?: Problema | null;
}

/**
 * Uma rodada da conversa em SSE (standards.md §5): mostra os deltas na hora, guarda a cotação recebida
 * e expõe o erro estruturado (itens não encontrados, sugestões a confirmar, falha).
 */
export function useConversa({ projetoId, aoTerminar, problemaInicial = null }: Opcoes) {
  const api = useApi();
  const cliente = useQueryClient();
  const [aoVivo, setAoVivo] = useState<MensagemAoVivo[]>([]);
  const [enviando, setEnviando] = useState(false);
  const [problema, setProblema] = useState<Problema | null>(problemaInicial);
  const cancelamento = useRef<AbortController | null>(null);

  // Ao sair da página, encerra o stream em andamento.
  useEffect(() => () => cancelamento.current?.abort(), []);

  const enviar = useCallback(
    async (texto: string) => {
      const conteudo = texto.trim();
      if (!conteudo || enviando) {
        return;
      }

      const controle = new AbortController();
      cancelamento.current = controle;
      setEnviando(true);
      setProblema(null);
      setAoVivo([{ papel: 'usuario', conteudo }]);

      const caminho = projetoId ? `/api/v1/projetos/${projetoId}/mensagens` : '/api/v1/projetos';
      const corpo = projetoId ? { conteudo } : { descricao: conteudo };
      let fim: { projetoId: string; status: StatusProjeto } | null = null;
      let problemaDaRodada: Problema | null = null;

      try {
        for await (const evento of api.conversar(caminho, corpo, controle.signal)) {
          if (evento.tipo === 'delta') {
            setAoVivo((atuais) => [
              ...atuais,
              { papel: 'assistente', conteudo: evento.dados.texto },
            ]);
          } else if (evento.tipo === 'cotacao') {
            cliente.setQueryData(chavesProjetos.detalhe(evento.dados.id), evento.dados);
          } else if (evento.tipo === 'erro') {
            problemaDaRodada = evento.dados;
            setProblema(evento.dados);
          } else {
            fim = evento.dados;
          }
        }
      } catch (erro) {
        if (controle.signal.aborted) {
          return;
        }
        setProblema(
          erro instanceof ErroApi
            ? erro.problema
            : { title: 'Não foi possível falar com o servidor.' },
        );
      } finally {
        setEnviando(false);
      }

      if (fim) {
        await cliente.invalidateQueries({ queryKey: chavesProjetos.todos });
        setAoVivo([]);
        aoTerminar?.({ ...fim, problema: problemaDaRodada });
      }
    },
    [api, cliente, enviando, projetoId, aoTerminar],
  );

  return { aoVivo, enviando, problema, enviar };
}
