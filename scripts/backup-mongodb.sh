#!/usr/bin/env bash
# Backup do banco da aplicação (ADR-009: o volume do MongoDB precisa de backup fora do contêiner).
# Gera backups/precificacao-AAAAMMDD-HHMMSS.archive.gz com coleções, índices e validadores.
#
# Uso: ./scripts/backup-mongodb.sh [quantidade a manter]   (padrão: 7 backups mais recentes)
# Agendamento sugerido (cron do host): 0 2 * * * /caminho/scripts/backup-mongodb.sh
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PASTA="$RAIZ/backups"
MANTER="${1:-7}"

cd "$RAIZ"
set -a; . ./.env; set +a
mkdir -p "$PASTA"

ARQUIVO="$PASTA/${MONGO_APP_DATABASE}-$(date +%Y%m%d-%H%M%S).archive.gz"
TEMPORARIO="$ARQUIVO.parcial"

# O dump sai pelo stdout do contêiner: nada fica gravado dentro dele.
docker compose exec -T mongodb mongodump --quiet \
  --username "$MONGO_ROOT_USER" --password "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin \
  --db "$MONGO_APP_DATABASE" --archive --gzip > "$TEMPORARIO"

# Só vira backup depois de completo, para uma falha no meio não deixar um arquivo truncado.
mv "$TEMPORARIO" "$ARQUIVO"
echo "Backup gravado: $ARQUIVO ($(du -h "$ARQUIVO" | cut -f1))"

# Retenção: apaga os mais antigos, mantendo os $MANTER mais recentes.
ls -1t "$PASTA"/"${MONGO_APP_DATABASE}"-*.archive.gz | tail -n +"$((MANTER + 1))" | while read -r antigo; do
  rm -f "$antigo"
  echo "Removido backup antigo: $antigo"
done
