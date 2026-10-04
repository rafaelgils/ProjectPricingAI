import { useState } from 'react';
import type { Usuario } from '../api/tipos.ts';
import { permissoes, usePerfil } from '../auth/usePerfil.ts';
import Aviso from '../componentes/Aviso.tsx';
import Paginacao from '../componentes/Paginacao.tsx';
import { formatarData, nomePapel } from '../formatacao.ts';
import {
  TAMANHO_PAGINA_USUARIOS,
  useDesativarUsuario,
  useUsuarios,
  type FiltroUsuarios,
} from '../usuarios/consultas.ts';
import FormularioUsuario from '../usuarios/FormularioUsuario.tsx';

type Edicao = { modo: 'novo' } | { modo: 'alterar'; usuario: Usuario } | null;

/** Gestão de usuários pelo Admin (RF10). Não há autocadastro; o Keycloak guarda os usuários. */
function PaginaUsuarios() {
  const { data: perfil } = usePerfil();
  const [filtro, setFiltro] = useState<FiltroUsuarios>({ busca: '', pagina: 1 });
  const [edicao, setEdicao] = useState<Edicao>(null);
  const podeGerenciar = perfil ? permissoes.gerenciarUsuarios(perfil.papeis) : false;

  if (perfil && !podeGerenciar) {
    return <Aviso tipo="erro" titulo="Você não tem acesso à gestão de usuários." />;
  }

  return perfil ? (
    <ListaUsuarios
      idLogado={perfil.keycloakId}
      filtro={filtro}
      setFiltro={setFiltro}
      edicao={edicao}
      setEdicao={setEdicao}
    />
  ) : (
    <p className="carregando">Carregando...</p>
  );
}

interface PropsLista {
  idLogado: string;
  filtro: FiltroUsuarios;
  setFiltro: React.Dispatch<React.SetStateAction<FiltroUsuarios>>;
  edicao: Edicao;
  setEdicao: (edicao: Edicao) => void;
}

/** Só monta (e só consulta a API) depois de confirmar que o usuário é Admin. */
function ListaUsuarios({ idLogado, filtro, setFiltro, edicao, setEdicao }: PropsLista) {
  const usuarios = useUsuarios(filtro);
  const desativar = useDesativarUsuario();

  const confirmarDesativacao = (usuario: Usuario) => {
    if (window.confirm(`Desativar "${usuario.usuario}"? Ele deixa de entrar no sistema.`)) {
      desativar.mutate(usuario.id);
    }
  };

  return (
    <section aria-labelledby="titulo-usuarios">
      <div className="titulo-com-acao">
        <h1 id="titulo-usuarios">Usuários</h1>
        {!edicao && (
          <button type="button" onClick={() => setEdicao({ modo: 'novo' })}>
            Novo usuário
          </button>
        )}
      </div>

      {edicao && (
        <FormularioUsuario
          key={edicao.modo === 'alterar' ? edicao.usuario.id : 'novo'}
          usuario={edicao.modo === 'alterar' ? edicao.usuario : undefined}
          aoConcluir={() => setEdicao(null)}
          aoCancelar={() => setEdicao(null)}
        />
      )}

      <form className="filtros" role="search" onSubmit={(e) => e.preventDefault()}>
        <label>
          Buscar
          <input
            type="search"
            value={filtro.busca}
            onChange={(e) => setFiltro({ busca: e.target.value, pagina: 1 })}
            placeholder="Usuário, nome ou e-mail"
          />
        </label>
      </form>

      {desativar.error && <Aviso tipo="erro" titulo={desativar.error.message} />}
      {usuarios.isPending && <p className="carregando">Carregando os usuários...</p>}
      {usuarios.error && <Aviso tipo="erro" titulo={usuarios.error.message} />}
      {usuarios.data && usuarios.data.itens.length === 0 && <p>Nenhum usuário encontrado.</p>}

      {usuarios.data && usuarios.data.itens.length > 0 && (
        <>
          <div className="tabela-rolavel">
            <table>
              <caption className="visualmente-oculto">Usuários do sistema</caption>
              <thead>
                <tr>
                  <th scope="col">Usuário</th>
                  <th scope="col">Nome</th>
                  <th scope="col">E-mail</th>
                  <th scope="col">Papel</th>
                  <th scope="col">Situação</th>
                  <th scope="col">Criado em</th>
                  <th scope="col">Ações</th>
                </tr>
              </thead>
              <tbody>
                {usuarios.data.itens.map((usuario) => (
                  <tr key={usuario.id}>
                    <td>
                      {usuario.usuario}
                      {usuario.id === idLogado && <small> (você)</small>}
                    </td>
                    <td>
                      {usuario.nome} {usuario.sobrenome}
                    </td>
                    <td>{usuario.email ?? '—'}</td>
                    <td>{nomePapel(usuario.papel)}</td>
                    <td>
                      <span className={`selo selo-${usuario.ativo ? 'ativo' : 'inativo'}`}>
                        {usuario.ativo ? 'Ativo' : 'Inativo'}
                      </span>
                    </td>
                    <td>{formatarData(usuario.criadoEm)}</td>
                    <td className="acoes-linha">
                      <button
                        type="button"
                        className="botao-secundario"
                        onClick={() => setEdicao({ modo: 'alterar', usuario })}
                        aria-label={`Alterar ${usuario.usuario}`}
                      >
                        Alterar
                      </button>
                      {usuario.ativo && usuario.id !== idLogado && (
                        <button
                          type="button"
                          className="botao-secundario"
                          onClick={() => confirmarDesativacao(usuario)}
                          aria-label={`Desativar ${usuario.usuario}`}
                        >
                          Desativar
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Paginacao
            pagina={filtro.pagina}
            total={usuarios.data.total}
            tamanho={TAMANHO_PAGINA_USUARIOS}
            aoMudar={(pagina) => setFiltro((atual) => ({ ...atual, pagina }))}
          />
        </>
      )}
    </section>
  );
}

export default PaginaUsuarios;
