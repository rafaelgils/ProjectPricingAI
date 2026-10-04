import { useEffect, type ReactNode } from 'react';
import { useAuth } from 'react-oidc-context';
import Aviso from '../componentes/Aviso.tsx';

/** Sem sessão, redireciona para o login no Keycloak (OIDC Authorization Code + PKCE, ADR-004). */
function ExigirLogin({ children }: { children: ReactNode }) {
  const auth = useAuth();
  const precisaEntrar =
    !auth.isLoading && !auth.isAuthenticated && !auth.activeNavigator && !auth.error;

  useEffect(() => {
    if (precisaEntrar) {
      void auth.signinRedirect();
    }
  }, [precisaEntrar, auth]);

  if (auth.error) {
    return (
      <Aviso tipo="erro" titulo="Não foi possível entrar">
        <p>{auth.error.message}</p>
        <button type="button" onClick={() => void auth.signinRedirect()}>
          Tentar de novo
        </button>
      </Aviso>
    );
  }

  if (!auth.isAuthenticated) {
    return <p className="carregando">Abrindo o login...</p>;
  }

  return <>{children}</>;
}

export default ExigirLogin;
