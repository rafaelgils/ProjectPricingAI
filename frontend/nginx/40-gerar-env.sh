#!/bin/sh
# Gera o /env.js na subida do contêiner a partir das variáveis de ambiente (plano, P5):
# a mesma imagem serve em qualquer ambiente. Executado pelo entrypoint oficial do Nginx.
set -eu

: "${API_URL:?defina API_URL}"
: "${OIDC_AUTHORITY:?defina OIDC_AUTHORITY}"
: "${OIDC_CLIENT_ID:?defina OIDC_CLIENT_ID}"

cat > /usr/share/nginx/html/env.js <<EOF
window.__CONFIG__ = {
  apiUrl: '${API_URL}',
  oidcAuthority: '${OIDC_AUTHORITY}',
  oidcClientId: '${OIDC_CLIENT_ID}',
};
EOF
