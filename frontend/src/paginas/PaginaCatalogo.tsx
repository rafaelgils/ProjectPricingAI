import { useState } from 'react';
import type { Material, StatusMaterial } from '../api/tipos.ts';
import { permissoes, usePerfil } from '../auth/usePerfil.ts';
import Aviso from '../componentes/Aviso.tsx';
import Paginacao from '../componentes/Paginacao.tsx';
import { formatarMoeda, nomeStatusMaterial, nomeUnidade } from '../formatacao.ts';
import {
  TAMANHO_PAGINA_CATALOGO,
  useInativarMaterial,
  useMateriais,
  type FiltroCatalogo,
} from '../materiais/consultas.ts';
import FormularioMaterial from '../materiais/FormularioMaterial.tsx';

type Edicao = { modo: 'novo' } | { modo: 'alterar'; material: Material } | null;

/**
 * Catálogo de materiais (RF02). Admin e cliente-interno consultam; só o Admin cadastra, altera,
 * inativa e reativa. O cliente-interno vê apenas os ativos (business-rules.md §3).
 */
function PaginaCatalogo() {
  const { data: perfil } = usePerfil();
  const podeGerenciar = perfil ? permissoes.gerenciarCatalogo(perfil.papeis) : false;
  const [filtro, setFiltro] = useState<FiltroCatalogo>({
    busca: '',
    categoria: '',
    status: '',
    pagina: 1,
  });
  const [edicao, setEdicao] = useState<Edicao>(null);
  const materiais = useMateriais(filtro);
  const inativar = useInativarMaterial();

  const filtrar = (mudanca: Partial<FiltroCatalogo>) =>
    setFiltro((atual) => ({ ...atual, ...mudanca, pagina: 1 }));

  if (perfil && !permissoes.verCatalogo(perfil.papeis)) {
    return <Aviso tipo="erro" titulo="Você não tem acesso ao catálogo." />;
  }

  const confirmarInativacao = (material: Material) => {
    if (
      window.confirm(
        `Inativar "${material.nome}"? Ele sai do catálogo, mas cotações já feitas não mudam.`,
      )
    ) {
      inativar.mutate(material.id);
    }
  };

  return (
    <section aria-labelledby="titulo-catalogo">
      <div className="titulo-com-acao">
        <h1 id="titulo-catalogo">Catálogo de materiais e serviços</h1>
        {podeGerenciar && !edicao && (
          <button type="button" onClick={() => setEdicao({ modo: 'novo' })}>
            Novo material
          </button>
        )}
      </div>

      {edicao && (
        <FormularioMaterial
          key={edicao.modo === 'alterar' ? edicao.material.id : 'novo'}
          material={edicao.modo === 'alterar' ? edicao.material : undefined}
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
            onChange={(e) => filtrar({ busca: e.target.value })}
            placeholder="Nome ou sinônimo"
          />
        </label>
        <label>
          Categoria
          <input
            value={filtro.categoria}
            onChange={(e) => filtrar({ categoria: e.target.value })}
          />
        </label>
        {podeGerenciar && (
          <label>
            Status
            <select
              value={filtro.status}
              onChange={(e) => filtrar({ status: e.target.value as StatusMaterial | '' })}
            >
              <option value="">Todos</option>
              <option value="ativo">Ativos</option>
              <option value="inativo">Inativos</option>
            </select>
          </label>
        )}
      </form>

      {inativar.error && <Aviso tipo="erro" titulo={inativar.error.message} />}
      {materiais.isPending && <p className="carregando">Carregando o catálogo...</p>}
      {materiais.error && <Aviso tipo="erro" titulo={materiais.error.message} />}
      {materiais.data && materiais.data.itens.length === 0 && <p>Nenhum material encontrado.</p>}

      {materiais.data && materiais.data.itens.length > 0 && (
        <>
          <div className="tabela-rolavel">
            <table>
              <caption className="visualmente-oculto">Materiais e serviços</caption>
              <thead>
                <tr>
                  <th scope="col">Nome</th>
                  <th scope="col">Sinônimos</th>
                  <th scope="col">Categoria</th>
                  <th scope="col">Unidade</th>
                  <th scope="col" className="numero">
                    Preço unitário
                  </th>
                  <th scope="col">Fornecedor</th>
                  {podeGerenciar && <th scope="col">Status</th>}
                  {podeGerenciar && <th scope="col">Ações</th>}
                </tr>
              </thead>
              <tbody>
                {materiais.data.itens.map((material) => (
                  <tr key={material.id}>
                    <td>
                      {material.nome}{' '}
                      <small>({material.tipo === 'servico' ? 'serviço' : 'material'})</small>
                    </td>
                    <td>{material.sinonimos.join(', ') || '—'}</td>
                    <td>{material.categoria}</td>
                    <td>{nomeUnidade(material.unidade)}</td>
                    <td className="numero">{formatarMoeda(material.precoUnitario)}</td>
                    <td>{material.fornecedor}</td>
                    {podeGerenciar && (
                      <td>
                        <span className={`selo selo-${material.status}`}>
                          {nomeStatusMaterial(material.status)}
                        </span>
                      </td>
                    )}
                    {podeGerenciar && (
                      <td className="acoes-linha">
                        <button
                          type="button"
                          className="botao-secundario"
                          onClick={() => setEdicao({ modo: 'alterar', material })}
                          aria-label={`Alterar ${material.nome}`}
                        >
                          Alterar
                        </button>
                        {material.status === 'ativo' && (
                          <button
                            type="button"
                            className="botao-secundario"
                            onClick={() => confirmarInativacao(material)}
                            aria-label={`Inativar ${material.nome}`}
                          >
                            Inativar
                          </button>
                        )}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Paginacao
            pagina={filtro.pagina}
            total={materiais.data.total}
            tamanho={TAMANHO_PAGINA_CATALOGO}
            aoMudar={(pagina) => setFiltro((atual) => ({ ...atual, pagina }))}
          />
        </>
      )}
    </section>
  );
}

export default PaginaCatalogo;
