export interface Configuracao {
  apiUrl: string;
  oidcAuthority: string;
  oidcClientId: string;
}

declare global {
  interface Window {
    __CONFIG__?: Partial<Configuracao>;
  }
}

/** Lida de /env.js na subida: a mesma imagem serve em qualquer ambiente (plano, P5). */
export function lerConfiguracao(): Configuracao {
  const config = window.__CONFIG__ ?? {};
  const { apiUrl, oidcAuthority, oidcClientId } = config;
  if (!apiUrl || !oidcAuthority || !oidcClientId) {
    throw new Error(
      'Configuração ausente: verifique o /env.js (apiUrl, oidcAuthority, oidcClientId).',
    );
  }
  return { apiUrl: apiUrl.replace(/\/$/, ''), oidcAuthority, oidcClientId };
}
