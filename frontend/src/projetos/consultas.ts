import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '../auth/contextoApi.ts';
import type { Mensagem, Pagina, Projeto, ProjetoResumo, StatusProjeto } from '../api/tipos.ts';

export const chavesProjetos = {
  todos: ['projetos'] as const,
  lista: (status: StatusProjeto | '', pagina: number) =>
    ['projetos', 'lista', status, pagina] as const,
  detalhe: (id: string) => ['projetos', 'detalhe', id] as const,
  mensagens: (id: string) => ['projetos', 'mensagens', id] as const,
};

export const TAMANHO_PAGINA = 20;

export function useProjetos(status: StatusProjeto | '', pagina: number) {
  const api = useApi();
  return useQuery({
    queryKey: chavesProjetos.lista(status, pagina),
    queryFn: ({ signal }) => {
      const filtros = new URLSearchParams({
        pagina: String(pagina),
        tamanho: String(TAMANHO_PAGINA),
      });
      if (status) filtros.set('status', status);
      return api.requisitar<Pagina<ProjetoResumo>>(`/api/v1/projetos?${filtros}`, {
        sinal: signal,
      });
    },
  });
}

export function useProjeto(id: string | undefined) {
  const api = useApi();
  return useQuery({
    queryKey: chavesProjetos.detalhe(id ?? ''),
    queryFn: ({ signal }) => api.requisitar<Projeto>(`/api/v1/projetos/${id}`, { sinal: signal }),
    enabled: Boolean(id),
  });
}

export function useMensagens(id: string | undefined) {
  const api = useApi();
  return useQuery({
    queryKey: chavesProjetos.mensagens(id ?? ''),
    queryFn: ({ signal }) =>
      api.requisitar<Mensagem[]>(`/api/v1/projetos/${id}/mensagens`, { sinal: signal }),
    enabled: Boolean(id),
  });
}

/** Exclusão lógica (RN04): o projeto vira somente leitura. */
export function useArquivarProjeto() {
  const api = useApi();
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.excluir(`/api/v1/projetos/${id}`),
    onSuccess: () => cliente.invalidateQueries({ queryKey: chavesProjetos.todos }),
  });
}
