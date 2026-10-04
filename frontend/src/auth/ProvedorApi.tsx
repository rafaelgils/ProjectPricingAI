import { useMemo, useRef, type ReactNode } from 'react';
import { useAuth } from 'react-oidc-context';
import { ClienteApi } from '../api/cliente.ts';
import { ContextoApi } from './contextoApi.ts';

interface Props {
  urlApi: string;
  children: ReactNode;
}

/** Cria o cliente da API com o token do usuário logado; o token mais recente é lido a cada chamada. */
function ProvedorApi({ urlApi, children }: Props) {
  const auth = useAuth();
  const token = useRef<string | undefined>(undefined);
  token.current = auth.user?.access_token;

  const api = useMemo(() => new ClienteApi(urlApi, () => token.current), [urlApi]);
  return <ContextoApi.Provider value={api}>{children}</ContextoApi.Provider>;
}

export default ProvedorApi;
