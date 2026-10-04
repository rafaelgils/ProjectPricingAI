import { Link } from 'react-router';

function PaginaNaoEncontrada() {
  return (
    <section>
      <h1>Página não encontrada</h1>
      <p>
        <Link to="/projetos">Ir para os projetos</Link>
      </p>
    </section>
  );
}

export default PaginaNaoEncontrada;
