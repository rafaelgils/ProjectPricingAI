import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '../auth/contextoApi.ts';
import type { DadosMaterial, Material, Pagina, StatusMaterial } from '../api/tipos.ts';

export interface FiltroCatalogo {
  busca: string;
  categoria: string;
  status: StatusMaterial | '';
  pagina: number;
}

export const TAMANHO_PAGINA_CATALOGO = 20;
const CHAVE = ['materiais'] as const;

export function useMateriais(filtro: FiltroCatalogo) {
  const api = useApi();
  return useQuery({
    queryKey: [...CHAVE, filtro],
    queryFn: ({ signal }) => {
      const parametros = new URLSearchParams({
        pagina: String(filtro.pagina),
        tamanho: String(TAMANHO_PAGINA_CATALOGO),
      });
      if (filtro.busca.trim()) parametros.set('busca', filtro.busca.trim());
      if (filtro.categoria.trim()) parametros.set('categoria', filtro.categoria.trim());
      if (filtro.status) parametros.set('status', filtro.status);
      return api.requisitar<Pagina<Material>>(`/api/v1/materiais?${parametros}`, { sinal: signal });
    },
  });
}

/** Cria (sem id) ou altera (com id e status) um material; só o Admin (RF02). */
export function useSalvarMaterial() {
  const api = useApi();
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      dados,
      status,
    }: {
      id?: string;
      dados: DadosMaterial;
      status?: StatusMaterial;
    }) =>
      id
        ? api.requisitar<Material>(`/api/v1/materiais/${id}`, {
            metodo: 'PUT',
            corpo: { ...dados, status },
          })
        : api.requisitar<Material>('/api/v1/materiais', { metodo: 'POST', corpo: dados }),
    onSuccess: () => cliente.invalidateQueries({ queryKey: CHAVE }),
  });
}

/** Exclusão lógica (RN04): o material sai do catálogo e continua no histórico. */
export function useInativarMaterial() {
  const api = useApi();
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.excluir(`/api/v1/materiais/${id}`),
    onSuccess: () => cliente.invalidateQueries({ queryKey: CHAVE }),
  });
}
