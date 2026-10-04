#!/usr/bin/env bash
# Restaura um backup gerado por backup-mongodb.sh. SUBSTITUI as coleções atuais do banco da aplicação.
#
# Uso: ./scripts/restaurar-mongodb.sh backups/precificacao-AAAAMMDD-HHMMSS.archive.gz --confirmar
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARQUIVO="${1:-}"

if [[ -z "$ARQUIVO" || ! -f "$ARQUIVO" ]]; then
  echo "Informe o arquivo de backup. Ex.: $0 backups/precificacao-20261004-020000.archive.gz --confirmar" >&2
  exit 1
fi
if [[ "${2:-}" != "--confirmar" ]]; then
  echo "A restauração apaga os dados atuais das coleções. Repita o comando com --confirmar." >&2
  exit 1
fi

cd "$RAIZ"
set -a; . ./.env; set +a

# --drop recria cada coleção com os validadores e índices guardados no backup.
docker compose exec -T mongodb mongorestore --quiet --drop \
  --username "$MONGO_ROOT_USER" --password "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin \
  --nsInclude "${MONGO_APP_DATABASE}.*" --archive --gzip < "$ARQUIVO"

echo "Backup restaurado: $ARQUIVO"
