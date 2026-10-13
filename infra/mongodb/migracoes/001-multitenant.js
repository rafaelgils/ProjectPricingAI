// Migração 001 — multi-tenant (ADR-011). Idempotente: pode ser executada mais de uma vez.
// Para bancos criados ANTES da refatoração (o script de init só roda na primeira subida do volume).
//
// 1. Atribui o tenant padrão aos documentos que ainda não têm tenantId.
// 2. Troca os índices únicos globais por índices compostos com tenantId.
// 3. Passa a exigir tenantId nos validadores das coleções.
//
// Uso (a partir da raiz do repositório):
//   ./scripts/migrar-para-multitenant.sh [tenant-padrao]

const tenantPadrao = process.env.TENANT_PADRAO || 'empresa-demo';
const nomeBanco = process.env.MONGO_APP_DATABASE;
const banco = db.getSiblingDB(nomeBanco);
const collationPt = { locale: 'pt', strength: 1 };

for (const colecao of ['materiais', 'projetos', 'conversas']) {
  const r = banco[colecao].updateMany({ tenantId: { $exists: false } }, { $set: { tenantId: tenantPadrao } });
  print(`${colecao}: ${r.modifiedCount} documento(s) receberam o tenant "${tenantPadrao}".`);
}

function trocarIndice(colecao, antigo, nome, chave, opcoes) {
  if (banco[colecao].getIndexes().some((i) => i.name === antigo)) {
    banco[colecao].dropIndex(antigo);
  }
  banco[colecao].createIndex(chave, { name: nome, ...opcoes });
}

trocarIndice('materiais', 'ux_materiais_nome', 'ux_materiais_tenant_nome',
  { tenantId: 1, nome: 1 }, { unique: true, collation: collationPt });
trocarIndice('materiais', 'ux_materiais_sinonimos', 'ux_materiais_tenant_sinonimos',
  { tenantId: 1, sinonimos: 1 },
  { unique: true, collation: collationPt, partialFilterExpression: { 'sinonimos.0': { $exists: true } } });
trocarIndice('projetos', 'ix_projetos_clienteId', 'ix_projetos_tenant_clienteId', { tenantId: 1, clienteId: 1 }, {});

// Acrescenta tenantId aos campos obrigatórios sem refazer o resto do validador.
for (const colecao of ['materiais', 'projetos', 'conversas']) {
  const info = banco.getCollectionInfos({ name: colecao })[0];
  const esquema = info.options.validator.$jsonSchema;
  if (!esquema.required.includes('tenantId')) {
    esquema.required.unshift('tenantId');
    esquema.properties.tenantId = { bsonType: 'string', minLength: 1 };
    banco.runCommand({ collMod: colecao, validator: { $jsonSchema: esquema } });
  }
}

print('Migração multi-tenant concluída.');
