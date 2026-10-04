#!/usr/bin/env bash
# Verificações da Definition of Done (standards.md §9). Substitui o CI no MVP.
# Uso: ./scripts/verificar.sh [--sem-docker]
set -euo pipefail

COBERTURA_MINIMA=70
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND="$RAIZ/backend"
FRONTEND="$RAIZ/frontend"
SEM_DOCKER=false
[[ "${1:-}" == "--sem-docker" ]] && SEM_DOCKER=true

etapa() { printf '\n==> %s\n' "$1"; }
falha() { printf '\nFALHOU: %s\n' "$1" >&2; exit 1; }

etapa "Backend: build (avisos como erro)"
dotnet build "$BACKEND/ProjectPricing.sln" -c Release --nologo -v quiet || falha "build do backend"

etapa "Regressão: valores esperados conferidos pelo cálculo independente"
node "$RAIZ/scripts/regressao/calcular-esperados.mjs" || falha "valores esperados da suíte de regressão"

etapa "Backend: testes e cobertura (mínimo ${COBERTURA_MINIMA}%)"
RESULTADOS="$(mktemp -d)"
trap 'rm -rf "$RESULTADOS"' EXIT
for projeto in "$BACKEND"/tests/*.Tests; do
  nome_teste="$(basename "$projeto")"
  # Cada projeto de teste mede só o seu assembly, para a soma não contar a mesma linha duas vezes.
  # Ficam de fora o Program.cs (composição) e o código gerado em obj/ (ex.: gerador do OpenAPI).
  modulo="${nome_teste%.Tests}"
  dotnet test "$projeto" -c Release --no-build --nologo -v quiet \
    --collect "XPlat Code Coverage" --results-directory "$RESULTADOS/$nome_teste" \
    -- "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=[$modulo]*" \
       "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile=**/Program.cs,**/obj/**" \
    || falha "testes de $nome_teste"
done

cobertas=0
validas=0
while IFS= read -r arquivo; do
  c="$(grep -o 'lines-covered="[0-9]*"' "$arquivo" | head -1 | grep -o '[0-9]*')"
  v="$(grep -o 'lines-valid="[0-9]*"' "$arquivo" | head -1 | grep -o '[0-9]*')"
  cobertas=$((cobertas + ${c:-0}))
  validas=$((validas + ${v:-0}))
done < <(find "$RESULTADOS" -name 'coverage.cobertura.xml')

if [[ "$validas" -eq 0 ]]; then
  echo "Cobertura: ainda não há linhas de código mensuráveis."
else
  percentual=$((cobertas * 100 / validas))
  echo "Cobertura de linhas: ${percentual}% (${cobertas}/${validas})"
  [[ "$percentual" -ge "$COBERTURA_MINIMA" ]] || falha "cobertura do backend abaixo de ${COBERTURA_MINIMA}%"
fi

etapa "Backend: pacotes vulneráveis"
vulneraveis="$(dotnet list "$BACKEND/ProjectPricing.sln" package --vulnerable --include-transitive 2>&1)"
if grep -qE '\b(Critical|High)\b' <<<"$vulneraveis"; then
  echo "$vulneraveis"
  falha "pacote com vulnerabilidade crítica ou alta no backend"
fi
echo "Nenhuma vulnerabilidade crítica ou alta."

etapa "Frontend: dependências"
(cd "$FRONTEND" && npm ci --no-audit --no-fund --loglevel=error) || falha "npm ci"

etapa "Frontend: lint e formatação"
(cd "$FRONTEND" && npm run --silent lint && npm run --silent format:check) || falha "lint ou formatação do frontend"

etapa "Frontend: testes de componentes"
(cd "$FRONTEND" && npm run --silent test:coverage) || falha "testes do frontend"

etapa "Frontend: build"
(cd "$FRONTEND" && npm run --silent build) || falha "build do frontend"

etapa "Frontend: pacotes vulneráveis"
(cd "$FRONTEND" && npm audit --audit-level=high) || falha "pacote com vulnerabilidade crítica ou alta no frontend"

if [[ "$SEM_DOCKER" == true ]]; then
  printf '\nAVISO: build das imagens pulado (--sem-docker). A DoD exige o docker compose build antes do PR.\n'
else
  etapa "Imagens: docker compose build"
  (cd "$RAIZ" && docker compose build) || falha "build das imagens"
fi

printf '\nTodas as verificações passaram.\n'
