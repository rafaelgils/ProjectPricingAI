import { createContext, useContext } from 'react';
import type { ClienteApi } from '../api/cliente.ts';

export const ContextoApi = createContext<ClienteApi | null>(null);

/** Cliente HTTP único do app (standards.md §4). */
export function useApi(): ClienteApi {
  const api = useContext(ContextoApi);
  if (!api) {
    throw new Error('useApi precisa estar dentro de ProvedorApi.');
  }
  return api;
}
