import { useCallback, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';
import type { Problema } from '../api/tipos.ts';
import Aviso from '../componentes/Aviso.tsx';
import { formatarMoeda, nomeStatusProjeto } from '../formatacao.ts';
import { useMensagens, useProjeto } from '../projetos/consultas.ts';
import PainelProblema, { textoConfirmacao } from '../projetos/PainelProblema.tsx';
import TabelaItens from '../projetos/TabelaItens.tsx';
import { useConversa, type ResultadoRodada } from '../projetos/useConversa.ts';

interface EstadoNavegacao {
  problema?: Problema | null;
}

/**
 * Cotação em conversa (RF03, RF04, RF08): a descrição cria o projeto, as mensagens seguintes o refinam.
 * A resposta chega em streaming (RNF02) e os valores vêm sempre da API (RN01).
 */
function PaginaCotacao() {
  const { id } = useParams();
  const navegar = useNavigate();
  const estado = useLocation().state as EstadoNavegacao | null;
  const [texto, setTexto] = useState('');

  const projeto = useProjeto(id);
  const mensagens = useMensagens(id);

  const aoTerminar = useCallback(
    (resultado: ResultadoRodada) => {
      if (!id) {
        // Projeto recém-criado: passa para a página dele, levando o erro da rodada, se houver.
        void navegar(`/projetos/${resultado.projetoId}`, {
          replace: true,
          state: { problema: resultado.problema },
        });
      }
    },
    [id, navegar],
  );
  const conversa = useConversa({ projetoId: id, aoTerminar, problemaInicial: estado?.problema });

  const arquivado = projeto.data?.status === 'arquivado';
  const novo = !id;

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    const conteudo = texto;
    setTexto('');
    void conversa.enviar(conteudo);
  };

  if (projeto.error) {
    return <Aviso tipo="erro" titulo={projeto.error.message} />;
  }

  return (
    <section className="cotacao" aria-labelledby="titulo-cotacao">
      <p>
        <Link to="/projetos">← Voltar aos projetos</Link>
      </p>
      <div className="titulo-com-acao">
        <h1 id="titulo-cotacao">
          {novo ? 'Nova cotação' : (projeto.data?.descricao ?? 'Projeto')}
        </h1>
        {projeto.data && (
          <span className={`selo selo-${projeto.data.status}`}>
            {nomeStatusProjeto(projeto.data.status)}
          </span>
        )}
      </div>

      {projeto.data && projeto.data.itens.length > 0 && (
        <>
          <p className="destaque-total" aria-live="polite">
            Valor estimado: <strong>{formatarMoeda(projeto.data.valorTotal ?? 0)}</strong>
          </p>
          <TabelaItens projeto={projeto.data} />
        </>
      )}

      <h2 id="titulo-conversa">Conversa</h2>
      <ol className="conversa" role="log" aria-live="polite" aria-labelledby="titulo-conversa">
        {mensagens.data?.map((mensagem, indice) => (
          <li key={`h-${indice}`} className={`mensagem mensagem-${mensagem.papel}`}>
            <span className="visualmente-oculto">
              {mensagem.papel === 'usuario' ? 'Você:' : 'Assistente:'}
            </span>
            {mensagem.conteudo}
          </li>
        ))}
        {conversa.aoVivo.map((mensagem, indice) => (
          <li key={`v-${indice}`} className={`mensagem mensagem-${mensagem.papel}`}>
            <span className="visualmente-oculto">
              {mensagem.papel === 'usuario' ? 'Você:' : 'Assistente:'}
            </span>
            {mensagem.conteudo}
          </li>
        ))}
      </ol>

      {conversa.problema && (
        <PainelProblema
          problema={conversa.problema}
          desabilitado={conversa.enviando || arquivado}
          aoConfirmar={(sugestao) => void conversa.enviar(textoConfirmacao(sugestao))}
        />
      )}

      {arquivado ? (
        <Aviso tipo="info" titulo="Este projeto está arquivado e só pode ser consultado." />
      ) : (
        <form className="formulario-conversa" onSubmit={enviar}>
          <label htmlFor="mensagem">
            {novo
              ? 'Descreva o projeto'
              : 'Ajuste a cotação (medidas, quantidades, incluir ou trocar materiais)'}
          </label>
          <textarea
            id="mensagem"
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            rows={novo ? 5 : 3}
            maxLength={4000}
            placeholder={
              novo
                ? 'Ex.: Quero cotar uma placa de trânsito 60 x 60 cm com película refletiva e cabeçote de metal.'
                : 'Ex.: Troque para 80 x 80 cm.'
            }
            disabled={conversa.enviando}
            required
          />
          <button type="submit" disabled={conversa.enviando || !texto.trim()}>
            {conversa.enviando ? 'Calculando...' : novo ? 'Cotar' : 'Enviar'}
          </button>
        </form>
      )}
    </section>
  );
}

export default PaginaCotacao;
