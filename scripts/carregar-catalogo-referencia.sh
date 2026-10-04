#!/usr/bin/env bash
# Carrega no MongoDB o catálogo de referência da F11 (infra/mongodb/referencia/catalogo-referencia.json),
# o mesmo usado pela suíte de regressão. Serve para demonstração e para o aceite.
# Só insere os materiais que ainda não existem (mesmo nome, sem diferenciar maiúsculas e acentos);
# os já cadastrados não são alterados. Um material cujo sinônimo já pertence a outro (RN10) é pulado e listado.
#
# Uso: ./scripts/carregar-catalogo-referencia.sh
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$RAIZ"
set -a; . ./.env; set +a

SCRIPT="const dados = $(cat infra/mongodb/referencia/catalogo-referencia.json);
$(cat <<'JS'
const collationPt = { locale: 'pt', strength: 1 };
let inseridos = 0;
let existentes = 0;
const conflitos = [];
for (const m of dados.materiais) {
  if (db.materiais.find({ nome: m.nome }).collation(collationPt).limit(1).hasNext()) {
    existentes++;
    continue;
  }
  // RN10: nome e sinônimos não podem repetir o nome nem o sinônimo de outro material. O índice único só
  // compara nome com nome e sinônimo com sinônimo; a comparação cruzada é feita aqui, como na API.
  const termos = [m.nome, ...m.sinonimos];
  const repetido = db.materiais
    .find({ $or: [{ nome: { $in: termos } }, { sinonimos: { $in: termos } }] })
    .collation(collationPt).limit(1).hasNext();
  if (repetido) {
    conflitos.push(m.nome);
    continue;
  }
  try {
    db.materiais.insertOne({
      nome: m.nome,
      sinonimos: m.sinonimos,
      tipo: m.tipo,
      categoria: m.categoria,
      unidade: m.unidade,
      precoUnitario: NumberDecimal(m.precoUnitario.toFixed(2)),
      fornecedor: m.fornecedor,
      status: 'ativo',
      atualizadoEm: new Date(),
    });
    inseridos++;
  } catch (erro) {
    if (erro.code !== 11000) throw erro;
    conflitos.push(m.nome);
  }
}
print(`Catálogo de referência: ${inseridos} inseridos, ${existentes} já existiam.`);
if (conflitos.length > 0) {
  print(`Pulados por nome ou sinônimo já usado em outro material (RN10): ${conflitos.join(', ')}`);
}
JS
)"

docker compose exec -T mongodb mongosh --quiet \
  --username "$MONGO_APP_USER" --password "$MONGO_APP_PASSWORD" --authenticationDatabase "$MONGO_APP_DATABASE" \
  "$MONGO_APP_DATABASE" --eval "$SCRIPT"
