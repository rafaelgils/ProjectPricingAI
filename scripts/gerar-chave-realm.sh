#!/usr/bin/env bash
# Gera o par RSA fixo do realm (ADR-010).
#   - Chave privada: vai para o .env (REALM_RSA_PRIVATE_KEY), que fica fora do Git.
#   - Chave pública: vai para o infra/kong/kong.yml, que é versionado (não é segredo).
# O Keycloak recebe a privada na importação do realm; o Kong valida o JWT com a pública.
#
# Uso: ./scripts/gerar-chave-realm.sh [--forcar]
#   --forcar  troca uma chave já existente (rotação). Depois, recrie o contêiner do Keycloak
#             e reinicie o Kong; tokens emitidos com a chave antiga deixam de valer.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARQUIVO_ENV="$RAIZ/.env"
ARQUIVO_KONG="$RAIZ/infra/kong/kong.yml"
VARIAVEL="REALM_RSA_PRIVATE_KEY"
TAMANHO_CHAVE=2048

[[ -f "$ARQUIVO_ENV" ]] || { echo "Crie o .env antes: cp .env.example .env" >&2; exit 1; }

if grep -qE "^${VARIAVEL}=.+" "$ARQUIVO_ENV" && [[ "${1:-}" != "--forcar" ]]; then
  echo "O .env já tem ${VARIAVEL}. Use --forcar para gerar outra chave (rotação)." >&2
  exit 1
fi

TEMP="$(mktemp -d)"
trap 'rm -rf "$TEMP"' EXIT

openssl genpkey -algorithm RSA -pkeyopt "rsa_keygen_bits:${TAMANHO_CHAVE}" -out "$TEMP/privada.pem" 2>/dev/null
openssl pkey -in "$TEMP/privada.pem" -pubout -out "$TEMP/publica.pem"

# PKCS#8 em uma linha só, sem cabeçalhos: o formato que o provedor "rsa" do Keycloak aceita
# e que cabe numa variável de ambiente usada como placeholder no JSON do realm.
privada_linha="$(grep -v -- '-----' "$TEMP/privada.pem" | tr -d '\r\n')"

if grep -qE "^${VARIAVEL}=" "$ARQUIVO_ENV"; then
  sed -i "s|^${VARIAVEL}=.*|${VARIAVEL}=${privada_linha}|" "$ARQUIVO_ENV"
else
  printf '\n%s=%s\n' "$VARIAVEL" "$privada_linha" >> "$ARQUIVO_ENV"
fi

# Troca o bloco PEM da chave pública no kong.yml, mantendo a indentação da linha BEGIN.
indentacao="$(grep -m1 -- '-----BEGIN PUBLIC KEY-----' "$ARQUIVO_KONG" | sed -E 's/^( *).*/\1/')"
awk -v ind="$indentacao" -v pem="$TEMP/publica.pem" '
  /-----BEGIN PUBLIC KEY-----/ { while ((getline linha < pem) > 0) print ind linha; pulando = 1; next }
  pulando && /-----END PUBLIC KEY-----/ { pulando = 0; next }
  !pulando { print }
' "$ARQUIVO_KONG" > "$TEMP/kong.yml"
cp "$TEMP/kong.yml" "$ARQUIVO_KONG"

echo "Chave gerada: privada em .env (${VARIAVEL}) e pública em infra/kong/kong.yml."
echo "Aplique com: docker compose up -d --force-recreate keycloak kong"
