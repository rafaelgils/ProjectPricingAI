import { Navigate, type RouteObject } from 'react-router';
import ExigirLogin from './auth/ExigirLogin.tsx';
import Layout from './componentes/Layout.tsx';
import PaginaNaoEncontrada from './paginas/PaginaNaoEncontrada.tsx';

/** Cada página vira um pacote separado, baixado só quando a rota é aberta (o pacote único passava de 560 kB). */
const pagina = (carregar: () => Promise<{ default: React.ComponentType }>) => async () => ({
  Component: (await carregar()).default,
});

export const rotas: RouteObject[] = [
  {
    path: '/',
    element: (
      <ExigirLogin>
        <Layout />
      </ExigirLogin>
    ),
    children: [
      { index: true, element: <Navigate to="/projetos" replace /> },
      { path: 'projetos', lazy: pagina(() => import('./paginas/PaginaProjetos.tsx')) },
      { path: 'projetos/novo', lazy: pagina(() => import('./paginas/PaginaCotacao.tsx')) },
      { path: 'projetos/:id', lazy: pagina(() => import('./paginas/PaginaCotacao.tsx')) },
      { path: 'catalogo', lazy: pagina(() => import('./paginas/PaginaCatalogo.tsx')) },
      { path: 'usuarios', lazy: pagina(() => import('./paginas/PaginaUsuarios.tsx')) },
      { path: '*', element: <PaginaNaoEncontrada /> },
    ],
  },
];
