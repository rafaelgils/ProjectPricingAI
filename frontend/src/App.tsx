import { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { WebStorageStateStore } from 'oidc-client-ts';
import { AuthProvider } from 'react-oidc-context';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { ErroApi } from './api/cliente.ts';
import ProvedorApi from './auth/ProvedorApi.tsx';
import type { Configuracao } from './config.ts';
import { rotas } from './rotas.tsx';

const roteador = createBrowserRouter(rotas);

/** Não insiste em erros do cliente (4xx): repetir não muda a resposta. */
const deveRepetir = (tentativas: number, erro: Error) =>
  !(erro instanceof ErroApi && erro.status < 500) && tentativas < 2;

function App({ config }: { config: Configuracao }) {
  const [clienteConsultas] = useState(
    () =>
      new QueryClient({
        defaultOptions: { queries: { retry: deveRepetir, refetchOnWindowFocus: false } },
      }),
  );

  return (
    <AuthProvider
      authority={config.oidcAuthority}
      client_id={config.oidcClientId}
      redirect_uri={`${window.location.origin}/`}
      post_logout_redirect_uri={`${window.location.origin}/`}
      scope="openid profile email"
      // Authorization Code + PKCE (ADR-004) com renovação silenciosa pelo refresh token.
      automaticSilentRenew
      userStore={new WebStorageStateStore({ store: window.sessionStorage })}
      onSigninCallback={() =>
        window.history.replaceState({}, document.title, window.location.pathname)
      }
    >
      <QueryClientProvider client={clienteConsultas}>
        <ProvedorApi urlApi={config.apiUrl}>
          <RouterProvider router={roteador} />
        </ProvedorApi>
      </QueryClientProvider>
    </AuthProvider>
  );
}

export default App;
