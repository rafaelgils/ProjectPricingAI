import type { ReactNode } from 'react';

interface Props {
  tipo: 'erro' | 'info' | 'sucesso';
  titulo?: string;
  children?: ReactNode;
}

/** Mensagem destacada. Erros usam role="alert" para leitores de tela (WCAG 2.1 AA). */
function Aviso({ tipo, titulo, children }: Props) {
  return (
    <div className={`aviso aviso-${tipo}`} role={tipo === 'erro' ? 'alert' : 'status'}>
      {titulo && <p className="aviso-titulo">{titulo}</p>}
      {children}
    </div>
  );
}

export default Aviso;
