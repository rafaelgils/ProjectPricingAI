#!/usr/bin/env bash
# Migra um banco existente para o modelo multi-tenant (ADR-011): atribui o tenant padrão aos documentos
# antigos, troca os índices únicos por índices compostos e passa a exigir tenantId.
# É idempotente. Faça um backup antes: ./scripts/backup-mongodb.sh
#
# Uso: ./scripts/migrar-para-multitenant.sh [tenant-padrao]   (padrão: empresa-demo)
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$RAIZ"
set -a; . ./.env; set +a
TENANT_PADRAO="${1:-empresa-demo}"

docker compose exec -T mongodb mongosh --quiet \
  --username "$MONGO_ROOT_USER" --password "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin \
  --eval "process.env.TENANT_PADRAO='${TENANT_PADRAO}'; process.env.MONGO_APP_DATABASE='${MONGO_APP_DATABASE}'; $(cat infra/mongodb/migracoes/001-multitenant.js)"
