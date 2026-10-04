// Executado pelo mongosh só na primeira subida do volume (docker-entrypoint-initdb.d).
// Coleções, validadores e índices conforme ADR-003 e business-rules.md §7.
// Os nomes dos campos e os códigos dos enums são os mesmos gravados pelo backend (camelCase, "m2", "ativo"...).

const nomeBanco = process.env.MONGO_APP_DATABASE;
const usuarioApp = process.env.MONGO_APP_USER;
const senhaApp = process.env.MONGO_APP_PASSWORD;

const banco = db.getSiblingDB(nomeBanco);

banco.createUser({
  user: usuarioApp,
  pwd: senhaApp,
  roles: [{ role: 'readWrite', db: nomeBanco }],
});

const unidades = ['m', 'm2', 'un', 'L', 'h'];
const textoObrigatorio = { bsonType: 'string', minLength: 1 };

banco.createCollection('materiais', {
  validator: {
    $jsonSchema: {
      bsonType: 'object',
      required: ['nome', 'sinonimos', 'tipo', 'categoria', 'unidade', 'precoUnitario', 'fornecedor', 'status', 'atualizadoEm'],
      properties: {
        nome: textoObrigatorio,
        sinonimos: { bsonType: 'array', items: textoObrigatorio },
        tipo: { enum: ['material', 'servico'] },
        categoria: textoObrigatorio,
        unidade: { enum: unidades },
        precoUnitario: { bsonType: 'decimal' },
        fornecedor: textoObrigatorio,
        status: { enum: ['ativo', 'inativo'] },
        atualizadoEm: { bsonType: 'date' },
      },
    },
  },
});

banco.createCollection('projetos', {
  validator: {
    $jsonSchema: {
      bsonType: 'object',
      required: ['clienteId', 'descricao', 'itens', 'status', 'criadoEm', 'alteradoEm', 'versao'],
      properties: {
        clienteId: textoObrigatorio,
        descricao: textoObrigatorio,
        itens: {
          bsonType: 'array',
          items: {
            bsonType: 'object',
            required: ['materialId', 'nomeSnapshot', 'quantidade', 'unidade', 'precoUnitarioSnapshot', 'subtotal'],
            properties: {
              materialId: { bsonType: 'objectId' },
              nomeSnapshot: textoObrigatorio,
              quantidade: { bsonType: 'decimal' },
              unidade: { enum: unidades },
              precoUnitarioSnapshot: { bsonType: 'decimal' },
              subtotal: { bsonType: 'decimal' },
            },
          },
        },
        valorTotal: { bsonType: ['decimal', 'null'] },
        status: { enum: ['rascunho', 'cotado', 'arquivado'] },
        criadoEm: { bsonType: 'date' },
        alteradoEm: { bsonType: 'date' },
        versao: { bsonType: 'int', minimum: 0 },
      },
    },
  },
});

banco.createCollection('conversas', {
  validator: {
    $jsonSchema: {
      bsonType: 'object',
      required: ['projetoId', 'mensagens'],
      properties: {
        projetoId: { bsonType: 'objectId' },
        mensagens: {
          bsonType: 'array',
          items: {
            bsonType: 'object',
            required: ['papel', 'conteudo', 'enviadaEm'],
            properties: {
              papel: { enum: ['usuario', 'assistente', 'tool'] },
              conteudo: { bsonType: 'string' },
              enviadaEm: { bsonType: 'date' },
            },
          },
        },
      },
    },
  },
});

// Ignora maiúsculas e acentos (RN10)
const collationPt = { locale: 'pt', strength: 1 };

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

banco.projetos.createIndex({ clienteId: 1 }, { name: 'ix_projetos_clienteId' });

banco.conversas.createIndex({ projetoId: 1 }, { name: 'ux_conversas_projetoId', unique: true });
