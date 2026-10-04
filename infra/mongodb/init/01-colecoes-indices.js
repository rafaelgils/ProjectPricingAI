// Executado pelo mongosh só na primeira subida do volume (docker-entrypoint-initdb.d).
// Coleções e índices conforme ADR-003. Validadores $jsonSchema entram na fase F3.

const nomeBanco = process.env.MONGO_APP_DATABASE;
const usuarioApp = process.env.MONGO_APP_USER;
const senhaApp = process.env.MONGO_APP_PASSWORD;

const banco = db.getSiblingDB(nomeBanco);

banco.createUser({
  user: usuarioApp,
  pwd: senhaApp,
  roles: [{ role: 'readWrite', db: nomeBanco }],
});

// Ignora maiúsculas e acentos (RN10)
const collationPt = { locale: 'pt', strength: 1 };

banco.createCollection('materiais');
banco.materiais.createIndex({ nome: 1 }, { name: 'ux_materiais_nome', unique: true, collation: collationPt });
// Parcial: materiais sem sinônimos (lista vazia) não colidem entre si no índice único.
banco.materiais.createIndex(
  { sinonimos: 1 },
  {
    name: 'ux_materiais_sinonimos',
    unique: true,
    collation: collationPt,
    partialFilterExpression: { 'sinonimos.0': { $exists: true } },
  },
);

banco.createCollection('projetos');
banco.projetos.createIndex({ clienteId: 1 }, { name: 'ix_projetos_clienteId' });

banco.createCollection('conversas');
banco.conversas.createIndex({ projetoId: 1 }, { name: 'ux_conversas_projetoId', unique: true });
