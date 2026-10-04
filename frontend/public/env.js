// Configuração lida em tempo de execução (plano, P5). No contêiner, este arquivo é
// gerado na subida a partir das variáveis de ambiente (nginx/40-gerar-env.sh).
window.__CONFIG__ = {
  apiUrl: 'http://localhost:8000',
  oidcAuthority: 'http://localhost:8080/realms/precificacao',
  oidcClientId: 'precificacao-frontend',
};
