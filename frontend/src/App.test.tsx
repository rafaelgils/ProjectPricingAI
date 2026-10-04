import { render, screen } from '@testing-library/react';
import App from './App.tsx';

describe('App', () => {
  it('exibe o título do sistema', () => {
    render(<App />);

    expect(
      screen.getByRole('heading', { level: 1, name: 'Cotação de Projetos' }),
    ).toBeInTheDocument();
  });
});
