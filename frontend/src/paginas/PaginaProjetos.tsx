import { useState } from 'react';
import { Link } from 'react-router';
import type { StatusProjeto } from '../api/tipos.ts';
import { permissoes, usePerfil } from '../auth/usePerfil.ts';
import Aviso from '../componentes/Aviso.tsx';
import Paginacao from '../componentes/Paginacao.tsx';
import { formatarData, formatarMoeda, nomeStatusProjeto } from '../formatacao.ts';
import { TAMANHO_PAGINA, useArquivarProjeto, useProjetos } from '../projetos/consultas.ts';

/** Lista, abertura e exclusão de projetos (RF07). O cliente vê os próprios; o Admin vê todos (RN07). */
function PaginaProjetos() {
  const [status, setStatus] = useState<StatusProjeto | ''>('');
  const [pagina, setPagina] = useState(1);
  const { data: perfil } = usePerfil();
  const projetos = useProjetos(status, pagina);
  const arquivar = useArquivarProjeto();

  const confirmarArquivamento = (id: string, descricao: string) => {
    if (
      window.confirm(`Excluir o projeto "${descricao}"? Ele fica arquivado, somente para leitura.`)
    ) {
      arquivar.mutate(id);
    }
  };

  return (
    <section aria-labelledby="titulo-projetos">
      <div className="titulo-com-acao">
        <h1 id="titulo-projetos">
          {perfil && permissoes.verTodosOsProjetos(perfil.papeis)
            ? 'Projetos de todos os clientes'
            : 'Meus projetos'}
        </h1>
        <Link className="botao" to="/projetos/novo">
          Nova cotação
        </Link>
      </div>

      <div className="filtros">
        <label>
          Status
          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value as StatusProjeto | '');
              setPagina(1);
            }}
          >
            <option value="">Todos</option>
            <option value="rascunho">Rascunho</option>
            <option value="cotado">Cotado</option>
            <option value="arquivado">Arquivado</option>
          </select>
        </label>
      </div>

      {arquivar.error && <Aviso tipo="erro" titulo={arquivar.error.message} />}
      {projetos.isPending && <p className="carregando">Carregando projetos...</p>}
      {projetos.error && <Aviso tipo="erro" titulo={projetos.error.message} />}

      {projetos.data && projetos.data.itens.length === 0 && (
        <p>Nenhum projeto ainda. Use “Nova cotação” para descrever o primeiro.</p>
      )}

      {projetos.data && projetos.data.itens.length > 0 && (
        <>
          <div className="tabela-rolavel">
            <table>
              <caption className="visualmente-oculto">Projetos</caption>
              <thead>
                <tr>
                  <th scope="col">Descrição</th>
                  <th scope="col">Status</th>
                  <th scope="col" className="numero">
                    Valor estimado
                  </th>
                  <th scope="col">Alterado em</th>
                  <th scope="col">Ações</th>
                </tr>
              </thead>
              <tbody>
                {projetos.data.itens.map((projeto) => (
                  <tr key={projeto.id}>
                    <td>
                      <Link to={`/projetos/${projeto.id}`}>{projeto.descricao}</Link>
                    </td>
                    <td>
                      <span className={`selo selo-${projeto.status}`}>
                        {nomeStatusProjeto(projeto.status)}
                      </span>
                    </td>
                    <td className="numero">
                      {projeto.valorTotal === null ? '—' : formatarMoeda(projeto.valorTotal)}
                    </td>
                    <td>{formatarData(projeto.alteradoEm)}</td>
                    <td>
                      {projeto.status !== 'arquivado' && (
                        <button
                          type="button"
                          className="botao-secundario"
                          onClick={() => confirmarArquivamento(projeto.id, projeto.descricao)}
                          aria-label={`Excluir o projeto ${projeto.descricao}`}
                        >
                          Excluir
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Paginacao
            pagina={pagina}
            total={projetos.data.total}
            tamanho={TAMANHO_PAGINA}
            aoMudar={setPagina}
          />
        </>
      )}
    </section>
  );
}

export default PaginaProjetos;
