import type { Problema, SugestaoMaterial } from '../api/tipos.ts';
import Aviso from '../componentes/Aviso.tsx';

interface Props {
  problema: Problema;
  desabilitado: boolean;
  aoConfirmar: (sugestao: SugestaoMaterial) => void;
}

/** Mensagem de confirmação enviada ao agente quando o cliente aceita uma sugestão (RN09). */
export const textoConfirmacao = (sugestao: SugestaoMaterial): string =>
  `Sim, pode usar "${sugestao.nome}" para "${sugestao.termo}".`;

/** Resultado da rodada que não virou cotação: o que falta e o que o cliente pode fazer. */
function PainelProblema({ problema, desabilitado, aoConfirmar }: Props) {
  if (problema.code === 'ITENS_NAO_ENCONTRADOS') {
    return (
      <Aviso tipo="erro" titulo="Alguns itens não estão no catálogo">
        <ul>
          {problema.itensNaoEncontrados?.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
        <p>Ajuste a descrição ou peça ao administrador o cadastro desses itens.</p>
      </Aviso>
    );
  }

  if (problema.code === 'ESCLARECIMENTO_NECESSARIO') {
    const sugestoes = problema.sugestoes ?? [];
    return (
      <Aviso tipo="info" titulo={problema.pergunta ?? 'Preciso de mais informações.'}>
        {sugestoes.length > 0 && (
          <ul className="sugestoes" aria-label="Sugestões de material">
            {sugestoes.map((sugestao) => (
              <li key={`${sugestao.termo}-${sugestao.materialId}`}>
                <span>
                  “{sugestao.termo}” → <strong>{sugestao.nome}</strong>{' '}
                  <small>
                    {sugestao.origem === 'agente'
                      ? '(sugerido pelo assistente)'
                      : `(${Math.round((sugestao.similaridade ?? 0) * 100)}% parecido)`}
                  </small>
                </span>
                <button type="button" disabled={desabilitado} onClick={() => aoConfirmar(sugestao)}>
                  Confirmar
                </button>
              </li>
            ))}
          </ul>
        )}
        {sugestoes.length === 0 && <p>Responda abaixo para continuar.</p>}
      </Aviso>
    );
  }

  return <Aviso tipo="erro" titulo={problema.title ?? 'Não foi possível concluir a cotação.'} />;
}

export default PainelProblema;
