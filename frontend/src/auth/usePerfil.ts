import { useQuery } from '@tanstack/react-query';
import type { Papel, Perfil } from '../api/tipos.ts';
import { useApi } from './contextoApi.ts';

/** Perfil e papéis vêm da API, que valida o token (ADR-004). */
export function usePerfil() {
  const api = useApi();
  return useQuery({
    queryKey: ['perfil'],
    queryFn: ({ signal }) => api.requisitar<Perfil>('/api/v1/usuarios/me', { sinal: signal }),
    staleTime: 5 * 60 * 1000,
  });
}

/** Matriz de permissões (business-rules.md §3). A API também confere; aqui só decide o que mostrar. */
export const permissoes = {
  verCatalogo: (papeis: Papel[]) => papeis.includes('admin') || papeis.includes('cliente-interno'),
  gerenciarCatalogo: (papeis: Papel[]) => papeis.includes('admin'),
  verTodosOsProjetos: (papeis: Papel[]) => papeis.includes('admin'),
};
