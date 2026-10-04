interface Props {
  pagina: number;
  total: number;
  tamanho: number;
  aoMudar: (pagina: number) => void;
}

function Paginacao({ pagina, total, tamanho, aoMudar }: Props) {
  const paginas = Math.max(1, Math.ceil(total / tamanho));
  if (paginas === 1) {
    return null;
  }

  return (
    <nav className="paginacao" aria-label="Paginação">
      <button
        type="button"
        className="botao-secundario"
        disabled={pagina <= 1}
        onClick={() => aoMudar(pagina - 1)}
      >
        Anterior
      </button>
      <span aria-live="polite">
        Página {pagina} de {paginas}
      </span>
      <button
        type="button"
        className="botao-secundario"
        disabled={pagina >= paginas}
        onClick={() => aoMudar(pagina + 1)}
      >
        Próxima
      </button>
    </nav>
  );
}

export default Paginacao;
