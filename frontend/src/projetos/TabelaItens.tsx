import type { Projeto } from '../api/tipos.ts';
import { formatarMoeda, formatarQuantidade, nomeUnidade } from '../formatacao.ts';

/** Itens da cotação com os valores que a API calculou (RN01, RN08); o navegador não recalcula. */
function TabelaItens({ projeto }: { projeto: Projeto }) {
  return (
    <div className="tabela-rolavel">
      <table>
        <caption>Itens da cotação</caption>
        <thead>
          <tr>
            <th scope="col">Material ou serviço</th>
            <th scope="col" className="numero">
              Quantidade
            </th>
            <th scope="col" className="numero">
              Preço unitário
            </th>
            <th scope="col" className="numero">
              Subtotal
            </th>
          </tr>
        </thead>
        <tbody>
          {projeto.itens.map((item) => (
            <tr key={item.materialId}>
              <td>{item.nome}</td>
              <td className="numero">
                {formatarQuantidade(item.quantidade)} {nomeUnidade(item.unidade)}
              </td>
              <td className="numero">{formatarMoeda(item.precoUnitario)}</td>
              <td className="numero">{formatarMoeda(item.subtotal)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <th scope="row" colSpan={3}>
              Valor estimado
            </th>
            <td className="numero total">{formatarMoeda(projeto.valorTotal ?? 0)}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}

export default TabelaItens;
