import { NavLink, Outlet } from 'react-router';
import { useAuth } from 'react-oidc-context';
import { permissoes, usePerfil } from '../auth/usePerfil.ts';

/** Cabeçalho com o menu conforme o papel (architecture.md §5.1) e a área principal. */
function Layout() {
  const auth = useAuth();
  const { data: perfil } = usePerfil();
  const papeis = perfil?.papeis ?? [];

  return (
    <>
      <a className="pular-conteudo" href="#conteudo">
        Pular para o conteúdo
      </a>
      <header className="cabecalho">
        <span className="marca">Cotação de Projetos</span>
        <nav aria-label="Principal">
          <ul>
            <li>
              <NavLink to="/projetos">Projetos</NavLink>
            </li>
            {permissoes.verCatalogo(papeis) && (
              <li>
                <NavLink to="/catalogo">Catálogo</NavLink>
              </li>
            )}
            {permissoes.gerenciarUsuarios(papeis) && (
              <li>
                <NavLink to="/usuarios">Usuários</NavLink>
              </li>
            )}
          </ul>
        </nav>
        <div className="usuario">
          {perfil && <span>{perfil.nome}</span>}
          <button
            type="button"
            className="botao-secundario"
            onClick={() => void auth.signoutRedirect()}
          >
            Sair
          </button>
        </div>
      </header>
      <main id="conteudo" tabIndex={-1}>
        <Outlet />
      </main>
    </>
  );
}

export default Layout;
