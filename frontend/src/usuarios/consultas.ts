import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '../auth/contextoApi.ts';
import type { DadosAlteracaoUsuario, DadosNovoUsuario, Pagina, Usuario } from '../api/tipos.ts';

export interface FiltroUsuarios {
  busca: string;
  pagina: number;
}

export const TAMANHO_PAGINA_USUARIOS = 20;
const CHAVE = ['usuarios'] as const;

export function useUsuarios(filtro: FiltroUsuarios) {
  const api = useApi();
  return useQuery({
    queryKey: [...CHAVE, filtro],
    queryFn: ({ signal }) => {
      const parametros = new URLSearchParams({
        pagina: String(filtro.pagina),
        tamanho: String(TAMANHO_PAGINA_USUARIOS),
      });
      if (filtro.busca.trim()) parametros.set('busca', filtro.busca.trim());
      return api.requisitar<Pagina<Usuario>>(`/api/v1/usuarios?${parametros}`, { sinal: signal });
    },
  });
}

/** Cria (sem id) ou altera (com id) um usuário; só o Admin (RF10). */
export function useSalvarUsuario() {
  const api = useApi();
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (
      pedido:
        { id: string; dados: DadosAlteracaoUsuario } | { id?: undefined; dados: DadosNovoUsuario },
    ) =>
      pedido.id
        ? api.requisitar<Usuario>(`/api/v1/usuarios/${pedido.id}`, {
            metodo: 'PUT',
            corpo: pedido.dados,
          })
        : api.requisitar<Usuario>('/api/v1/usuarios', { metodo: 'POST', corpo: pedido.dados }),
    onSuccess: () => cliente.invalidateQueries({ queryKey: CHAVE }),
  });
}

/** Exclusão lógica: o usuário é desativado no Keycloak e deixa de entrar no sistema. */
export function useDesativarUsuario() {
  const api = useApi();
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.excluir(`/api/v1/usuarios/${id}`),
    onSuccess: () => cliente.invalidateQueries({ queryKey: CHAVE }),
  });
}
