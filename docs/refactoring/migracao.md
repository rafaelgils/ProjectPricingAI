# Migração para o modelo multi-tenant

Quem já tem o ambiente criado antes da branch `refactoring` precisa de três passos. Ambiente novo (volume vazio) não precisa de nada:
o script de inicialização do MongoDB e a importação do realm já saem no formato novo.

## 1. Backup

```bash
./scripts/backup-mongodb.sh
```

## 2. Banco de dados

Atribui o tenant padrão aos documentos antigos, troca os índices únicos por compostos com `tenantId`, passa a exigir `tenantId` nos
validadores e cria os índices da idempotência. É idempotente.

```bash
./scripts/migrar-para-multitenant.sh empresa-demo
```

O argumento é o tenant que recebe os dados existentes (padrão: `empresa-demo`, o mesmo dos usuários de teste).

## 3. Keycloak

O realm só é importado na criação do contêiner. Para ganhar o atributo `tenant_id`, o *mapper* da claim e o perfil de usuário declarativo:

```bash
docker compose up -d --force-recreate keycloak kong
```

Atenção: recriar o contêiner apaga os usuários criados pela aplicação (comportamento já documentado no guia de debug). Se isso não for
aceitável, use o console do Keycloak: crie o atributo `tenant_id` no perfil de usuário, adicione um *User Attribute Mapper*
(`tenant_id` → claim `tenant_id`, tipo String, no token de acesso) ao cliente `precificacao-frontend` e preencha o atributo em cada usuário.

Usuário sem o atributo recebe `403 ACESSO_NEGADO` em toda rota protegida.

## Variáveis novas no `.env`

| Variável | Uso |
| --- | --- |
| `TENANT_ID_DEMO` | Tenant dos usuários de teste importados no realm (padrão `empresa-demo`) |
| `TENANT_ID` (só no script do catálogo) | Tenant que recebe o catálogo de referência |

Valores `troque-…` no `.env` agora impedem o backend de subir: troque todos os segredos.
