import { Navigate, type RouteObject } from 'react-router';
import ExigirLogin from './auth/ExigirLogin.tsx';
import Layout from './componentes/Layout.tsx';
import PaginaCatalogo from './paginas/PaginaCatalogo.tsx';
import PaginaCotacao from './paginas/PaginaCotacao.tsx';
import PaginaNaoEncontrada from './paginas/PaginaNaoEncontrada.tsx';
import PaginaProjetos from './paginas/PaginaProjetos.tsx';
import PaginaUsuarios from './paginas/PaginaUsuarios.tsx';

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
      { path: 'projetos', element: <PaginaProjetos /> },
      { path: 'projetos/novo', element: <PaginaCotacao /> },
      { path: 'projetos/:id', element: <PaginaCotacao /> },
      { path: 'catalogo', element: <PaginaCatalogo /> },
      { path: 'usuarios', element: <PaginaUsuarios /> },
      { path: '*', element: <PaginaNaoEncontrada /> },
    ],
  },
];
