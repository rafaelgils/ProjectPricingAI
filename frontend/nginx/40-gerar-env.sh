#!/bin/sh
# Executado pelo entrypoint oficial do Nginx na subida do contêiner (plano, P5 e F9):
#   - /env.js com a configuração do app: a mesma imagem serve em qualquer ambiente;
#   - cabeçalhos de segurança, com a política de conteúdo (CSP) liberando só a API e o Keycloak.
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

# Origem (esquema://host:porta) de cada serviço que o navegador chama.
origem() { echo "$1" | sed -E 's#^(https?://[^/]+).*#\1#'; }
ORIGEM_API="$(origem "$API_URL")"
ORIGEM_OIDC="$(origem "$OIDC_AUTHORITY")"

cat > /etc/nginx/gerado/seguranca.conf <<EOF
add_header Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self' ${ORIGEM_API} ${ORIGEM_OIDC}; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-Frame-Options "DENY" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
add_header Permissions-Policy "camera=(), microphone=(), geolocation=()" always;
EOF
