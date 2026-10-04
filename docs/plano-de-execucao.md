# Plano de execução — Sistema de Cotação de Projetos

> Versão 0.3 · 04/10/2026
> Baseado em `.ai/architecture.md`, `.ai/business-rules.md`, `.ai/standards.md` e `.ai/tech-stack.md` (todos na v0.7, já com as respostas das dúvidas D1 a D25 e dos pontos Q1 a Q8).
> O desenvolvimento é feito por IA. Por isso o plano não traz equipe nem datas: cada fase vira um ou mais Pull Requests, revisados e aprovados por uma pessoa.
> Quando a documentação não define um ponto, o plano adota uma **premissa (P#)**. Os pontos que ainda dependem de resposta estão na seção 8 (**Q#**).

---

## 1. Objetivo e escopo

Entregar a **release 1.0.0 (MVP)** rodando localmente com `docker compose up`, em HTTP.

| Prioridade | Requisitos | Situação no plano |
| --- | --- | --- |
| Must | RF01 a RF06 | Dentro do MVP |
| Should | RF07, RF08 | Dentro do MVP |
| Could | RF10 (gestão de usuários) | Fase opcional F10 |
| Fora do MVP | RF09 (PDF) | Não será feito |

**Valem para todas as fases:**
- RN01 a RN12.
- RNF01, RNF02, RNF04 e RNF05 (o RNF03 foi retirado).
- ADR-001 a ADR-010.

**Fora do escopo neste momento:**
- Pipeline de CI.
- Ambientes de homologação e produção.
- HTTPS no ambiente local.
- Limite de uso da Claude API.

## 2. Premissas adotadas

| ID | Premissa | Motivo |
| --- | --- | --- |
| P1 | O Backend é uma solução .NET com 4 projetos (`Dominio`, `Aplicacao`, `Infraestrutura`, `Api`), mais 1 projeto de testes para cada um | SOLID e inversão de dependência (`standards.md` §2) |
| P2 | O Servidor MCP roda dentro do contêiner `backend` (`ModelContextProtocol.AspNetCore`), no endpoint interno `/mcp`, que o Kong não roteia. O agente usa um `McpClient` e repassa o JWT do usuário, para que as *policies* também valham nas tools | ADR-006 e ADR-009 |
| P3 | Valores monetários e quantidades usam `decimal` / `Decimal128`. As datas são gravadas em UTC e a API as devolve em `-03:00` | `standards.md` §3 e §5 |
| P4 | Paginação: `tamanho` tem padrão 20 e máximo 100 | `standards.md` §5 não define os valores |
| P5 | A configuração do Frontend é lida na subida do contêiner (`env.js` gerado pelo Nginx), e não no build do Vite | Uma imagem por release (ADR-009) |
| P6 | O SSE é lido no Frontend com `fetch` e leitura de stream, porque o `EventSource` não envia o cabeçalho `Authorization` | `tech-stack.md` §4 |
| P7 | Portas locais: Frontend `http://localhost:3000`, Kong `http://localhost:8000` e Keycloak `http://localhost:8080`. Backend e MongoDB não publicam portas | Arquitetura §1.1 |
| P8 | Chave fixa do realm (ADR-010): o par RSA é gerado por um script com `openssl`. A chave privada fica no `.env` e entra no realm por *placeholder* na importação. A chave pública, que não é segredo, fica versionada no `kong.yml` | Nenhum segredo no Git nem na imagem |
| P9 | A similaridade da RN09 (Levenshtein normalizado) é calculada em memória sobre os materiais ativos, sem índice de busca no MongoDB | Volume de catálogo pequeno no MVP |
| P10 | O prompt de sistema orienta o LLM a usar, como termo de busca, as palavras do próprio cliente para cada item | RN09 compara o termo com o nome do material. Ver Q9 |

## 3. Estrutura do repositório

```text
ProjectPricingAI/
├── .ai/                          # regras (v0.6)
├── docs/                         # este plano e guias de usuário
├── .editorconfig
├── docker-compose.yml
├── .env.example                  # todas as variáveis, sem valores secretos
├── scripts/
│   ├── gerar-chave-realm.sh      # par RSA da chave fixa (P8)
│   └── verificar.sh              # build, lint, testes, cobertura e análise de dependências
├── infra/
│   ├── keycloak/realm-precificacao.json
│   ├── kong/kong.yml
│   └── mongodb/init/01-colecoes-indices.js
├── backend/
│   ├── Dockerfile                # multi-stage: sdk:10.0 → aspnet:10.0
│   ├── ProjectPricing.sln
│   ├── src/
│   │   ├── ProjectPricing.Dominio/
│   │   ├── ProjectPricing.Aplicacao/
│   │   ├── ProjectPricing.Infraestrutura/
│   │   └── ProjectPricing.Api/
│   ├── prompts/                  # prompt de sistema e schema de saída versionados
│   └── tests/
│       ├── ...Dominio.Tests / ...Aplicacao.Tests / ...Api.Tests
│       └── Regressao/            # 30 descrições + catálogo de referência + respostas fixas do mock
└── frontend/
    ├── Dockerfile                # multi-stage: node LTS → nginx estável
    ├── nginx/default.conf
    └── src/
```

**Sem CI:** o script `scripts/verificar.sh` reúne as verificações que a DoD (`standards.md` §9) exige antes de cada PR:
- `dotnet build` com avisos como erro.
- `dotnet test` com cobertura de 70% ou mais.
- `dotnet list package --vulnerable`.
- `npm run lint`, Prettier, `vitest` e `npm audit`.
- `docker compose build`.

## 4. Fases e entregas

Cada fase termina em PRs `feature/<id>-<descricao>`, com *squash merge*, Conventional Commits e a Definition of Done.

### F1 — Fundação do repositório

1. Criar a estrutura da seção 3, o `.editorconfig`, o `.gitignore` (inclui `.env`) e o `.env.example`.
2. Criar a solução .NET 10 com `Nullable` habilitado, analisadores ativos e `TreatWarningsAsErrors`.
3. Criar o projeto Vite + React 19 + TypeScript 5 `strict`, com ESLint e Prettier.
4. Criar o `scripts/verificar.sh`.
5. Criar o `docker-compose.yml` com os 5 serviços:
   - Imagens com tag fixa, nunca `latest`.
   - *Healthchecks* e `depends_on` com `condition: service_healthy`.
   - Volume nomeado para o MongoDB.
   - Portas como em P7.

6. Instalar as ferramentas de teste e build aprovadas no `tech-stack.md` §4 e §5: `coverlet.collector`, `@vitest/coverage-v8`, `jsdom`, `@testing-library/jest-dom`, `@testing-library/user-event`, `@vitejs/plugin-react` e `typescript-eslint`.

**Critério de aceite:** `docker compose up` sobe os 5 contêineres saudáveis e o `verificar.sh` passa.

### F2 — Identidade (Keycloak) e borda (Kong)

**Keycloak 26.x** em `start-dev` (HTTP), com o realm `precificacao` importado por `--import-realm`:

- *Realm roles*: `admin`, `cliente-interno` e `cliente-externo`.
- Sem autocadastro (`registrationAllowed=false`).
- Cliente `precificacao-frontend`: público, PKCE S256, redirect para `http://localhost:3000/*`.
- Cliente `precificacao-api`: *audience mapper* que inclui `aud=precificacao-api` no token.
- Cliente `precificacao-admin`: confidencial, com *service account* e os papéis `view-users` e `manage-users`. É usado pelo Backend para a Admin API.
- Provedor de chave RSA fixo (P8).
- Um usuário de teste por papel, só no ambiente local.
- `KC_HOSTNAME=http://localhost:8080` com `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`. Sem essa opção, os metadados lidos pela rede interna apontariam o `jwks_uri` para `localhost`, que dentro do contêiner `backend` é o próprio contêiner.
- O Backend lê os metadados por `http://keycloak:8080` (`MetadataAddress`), valida o `iss` público e usa `RequireHttpsMetadata=false`, só no ambiente local.

**Kong 3.9 OSS** sem banco (`kong.yml`):

- Serviço `backend` e rota `/api/v1` com `strip_path=false`.
- Plugin `cors` liberando só `http://localhost:3000`.
- Plugin `jwt` com RS256 e `claims_to_verify=exp`. O consumidor tem `key` igual ao `iss` e `rsa_public_key` igual à chave fixa (ADR-010).
- Para o SSE:
  - Rotas `POST /api/v1/projetos` e `POST /api/v1/projetos/{id}/mensagens` com `response_buffering=false`.
  - `read_timeout` de 120 s no serviço `backend`. No Kong, o *timeout* é configurado no serviço, não na rota.
- Rota `/openapi/v1.json`.

**Critério de aceite:**
- O login PKCE devolve um token com `realm_access.roles`.
- Uma chamada sem token ao Kong recebe `401`.
- Uma chamada com token chega ao Backend.

### F3 — Fundação do Backend

- `Api`:
  - `JwtBearer` com *policies* `PodeGerenciarCatalogo` (admin), `PodeConsultarCatalogo` (admin e cliente-interno), `PodeCotar` (os 3 papéis) e `PodeGerenciarUsuarios` (admin).
  - Nomes dos papéis centralizados numa classe de constantes.
  - `IUsuarioAtual`, que lê o `keycloakId` da claim `sub`.
  - Tratamento global de erros: Problem Details com `code` (`standards.md` §5).
  - OpenAPI nativo em `/openapi/v1.json`.
  - *Healthcheck*.
- `Dominio`:
  - Entidades `Material`, `Projeto` (com `criadoEm` e `alteradoEm`), `ItemProjeto`, `Conversa` e `Mensagem`.
  - Exceções de domínio, cada uma ligada a um `code`:

| Exceção | Status e `code` |
| --- | --- |
| `MaterialDuplicadoException` | 409 |
| `ItensNaoEncontradosException` | 422 `ITENS_NAO_ENCONTRADOS` |
| `EsclarecimentoNecessarioException` | 422 `ESCLARECIMENTO_NECESSARIO` |
| `ProjetoArquivadoException` | 422 `PROJETO_ARQUIVADO`, "Este projeto está inativo." |
| `FalhaProcessamentoException` | 500 `FALHA_PROCESSAMENTO`, com a mensagem fixa da RN12 |
| `RecursoNaoEncontradoException` | 404 |

- `Infraestrutura`:
  - MongoDB.Driver 3.x, com `decimal` serializado como `Decimal128`.
  - Repositórios `IMaterialRepository`, `IProjetoRepository` e `IConversaRepository`.
- MongoDB (`01-colecoes-indices.js`):
  - Coleções com validador `$jsonSchema`.
  - `materiais.nome` único com *collation* `{ locale: "pt", strength: 1 }`.
  - Índice em `projetos.clienteId` e índice único em `conversas.projetoId`.
- `GET /api/v1/usuarios/me`.

**Critério de aceite:** `/usuarios/me` responde através do Kong, e os testes das *policies* e do mapeamento de exceções passam.

### F4 — Catálogo de materiais (RF02, RN04, RN10)

- Endpoints `GET/POST/PUT/DELETE /api/v1/materiais`:
  - Filtros `busca`, `categoria`, `status`, `pagina` e `tamanho`; a resposta traz `total`.
  - O `PUT` também reativa (`status=ativo`).
- `FluentValidation`:
  - Nome, tipo (`material` ou `servico`), categoria (texto livre), unidade (`m|m2|un|L|h`) e fornecedor são obrigatórios.
  - `precoUnitario > 0` com no máximo **2 casas**.
- Nome duplicado (sem diferenciar maiúsculas e acentos, incluindo inativos) retorna `409`.
- `DELETE` inativa o material. `POST` responde `201` com `Location`. `atualizadoEm` muda a cada alteração.
- Meta de desempenho: p95 de até 500 ms.

**Critério de aceite:**
- Fluxo 5.2 da arquitetura funcionando.
- Cliente externo recebe `403`.
- Testes do `MaterialService` e dos validadores passam, incluindo os casos de duplicidade (maiúsculas, acentos, inativo) e de reativação.

### F5 — Serviço de Precificação (RN01, RN05, RN08)

Esta fase tem prioridade de cobertura (`standards.md` §7).

- `IConversorUnidades`: tabela da RN05, com uma regra por unidade de destino (aberto/fechado). A unidade `m2` aceita área direta ou largura × altura.
- Arredondamento (RN08), com uma função para cada passo:
  - `ParaCalculo(decimal)`: `Math.Round(valor, 3, MidpointRounding.AwayFromZero)`.
  - `ParaGravacao(decimal)`: de 3 para 2 casas, com o limiar `LimiarArredondamento = 6` na 3ª casa. Vale para reais e quantidades.
- `ServicoPrecificacao.Calcular(itens)`:
  - Para cada item: converte a unidade e leva a quantidade a 3 casas.
  - Calcula `quantidade (3 casas) × precoUnitarioSnapshot`, leva o resultado a 3 casas e depois a 2 casas, que é o subtotal.
  - Grava a quantidade com 2 casas.
  - Total = soma dos subtotais com 2 casas.
  - Sem margem e sem impostos.
- Recálculo no refinamento: só os itens alterados, incluídos ou trocados são recalculados; os demais mantêm o subtotal gravado (RN02).
- Testes:
  - Os 7 casos da tabela RN08, incluindo:
    - 9,57375 → R$ 9,57.
    - 10 min → 0,167 h no cálculo e 0,17 h na gravação.
    - 0,167 h × R$ 85,00 → R$ 14,19.
  - O exemplo da placa: 60 × 60 cm → 0,36 m², e total de R$ 105,90.
  - Cada linha da tabela RN05.
  - Unidade incompatível gera exceção.
  - Quantidade zero ou negativa gera exceção.

**Critério de aceite:** cobertura do serviço perto de 100% e todos os exemplos de `business-rules.md` reproduzidos.

### F6 — Servidor MCP e Agente de Projetos (RF03, RF05, RF08, RN03, RN06, RN09, RN12)

**Servidor MCP** (P2). As tools chamam serviços de domínio, nunca o banco:

- `buscarMateriais(termos[])` devolve, para cada termo, o material ativo mais próximo e a similaridade pelo Levenshtein normalizado da RN09, implementado no Backend sem biblioteca externa (P9). Cada termo é classificado em `encontrado` (100%), `aConfirmar` (de 80% a menos de 100%) ou `naoEncontrado` (abaixo de 80%), usando a constante `LimiarSimilaridade = 0.80`.
- `calcular(itens[])` chama o `ServicoPrecificacao`.
- `salvarProjeto(projetoId, itens, total)` grava o snapshot, muda o status para `cotado` e atualiza `alteradoEm`.

**Cliente do LLM:**

- SDK `Anthropic` exposto como `IChatClient`, com o modelo `claude-opus-5-5` vindo da configuração e a chave em variável de ambiente.
- `Microsoft.Extensions.Http.Resilience` aplica *retry* e *timeout*.

**Agente** (`AgenteProjetos`):

1. Envia ao LLM o prompt versionado, o histórico e **só** a tool `buscarMateriais` (ADR-006).
2. Exige saída estruturada `{ tipo: "itens" | "esclarecimento", itens[], pergunta }`.
   - Cada item traz `materialId`, `termo`, e `quantidade` ou `medidas` (largura e altura), mais a `unidadeInformada`.
3. Valida no Backend:
   - O schema.
   - O `materialId`, que precisa ter vindo de `buscarMateriais` nesta rodada ou de uma confirmação anterior na conversa.
   - Se a unidade é conversível.
   - Se a quantidade é maior que zero.
4. Decide, nesta ordem:
   1. Algum termo `naoEncontrado` → `ITENS_NAO_ENCONTRADOS`, sem valor parcial (RN03).
   2. Algum termo `aConfirmar` (sem confirmação anterior) → `ESCLARECIMENTO_NECESSARIO` com as `sugestoes` (RN09).
   3. Tipo `esclarecimento` (falta medida) → `ESCLARECIMENTO_NECESSARIO` com a `pergunta` (RN06).
   4. Caso contrário, chama `calcular` e depois `salvarProjeto` pelo código.
5. Se o LLM falhar, der *timeout* ou devolver saída inválida duas vezes → `FALHA_PROCESSAMENTO` (500). O projeto continua no estado anterior (RN12).
6. Grava todas as mensagens na conversa, inclusive as confirmações de material.

**Testes unitários** (Moq no `IChatClient` e nos repositórios):

- Fluxo feliz.
- Termo não encontrado.
- Termo a confirmar, e depois confirmado.
- Precedência entre não encontrado e a confirmar.
- Medida faltando.
- Saída inválida e falha do LLM.
- `materialId` inventado pelo LLM.
- `calcular` e `salvarProjeto` nunca oferecidos ao LLM.
- Testes da função de similaridade:
  - Normalização (maiúsculas, acentos e espaços).
  - O exemplo "película reflexiva" × "Película refletiva" ≈ 94%.
  - Os limites 79,9%, 80% e 100%.

### F7 — Projetos e conversas (RF03, RF04, RF06, RF07, RN02, RN07, RN11)

- `POST /api/v1/projetos` (os 3 papéis):
  - Valida a entrada (`400` antes do stream).
  - Cria o projeto em `rascunho` e a conversa.
  - Responde `201` com `Location` e abre o SSE.
  - Eventos `delta`, `cotacao` ou `erro`, e por fim `fim` (`standards.md` §5).
  - Primeiro byte em até 3 s (RNF02).
- `GET /api/v1/projetos` e `GET /api/v1/projetos/{id}`: o cliente vê os próprios e o Admin vê todos; projeto de outro cliente retorna `404` (RN07).
- `DELETE /api/v1/projetos/{id}`: arquiva o projeto e atualiza `alteradoEm`.
- `POST /api/v1/projetos/{id}/mensagens` (dono):
  - Projeto arquivado → `422 PROJETO_ARQUIVADO` antes do stream (RN11).
  - Projeto em `rascunho`: o cliente reformula a descrição ou confirma materiais, e o agente tenta cotar de novo.
  - Projeto `cotado`: o agente pode alterar quantidades e incluir, remover ou trocar materiais (RN02).
    - Item que já existe mantém o `precoUnitarioSnapshot`.
    - Item incluído ou trocado passa pela RN09 e usa o preço atual do catálogo, que fica congelado.
    - Só os itens alterados são recalculados; o total e o `alteradoEm` são atualizados.
    - Pedidos para alterar o preço de um material são recusados com `ESCLARECIMENTO_NECESSARIO`, explicando que o preço vem do catálogo.
    - Se o refinamento cair em RN03, RN06, RN09 ou RN12, a cotação anterior é mantida.
- `GET /api/v1/projetos/{id}/mensagens`: histórico.
- **Não existe `PUT /projetos/{id}`.**
- Controle de concorrência otimista (campo `versao`).

**Testes:**
- RN02:
  - Mudar o preço do material não altera a cotação.
  - Item que já existe mantém o preço congelado; item incluído ou trocado usa o preço atual.
  - Remover um item tira o subtotal dele do total.
  - Pedido de alteração de preço é recusado.
- RN07: visibilidade.
- RN11: arquivado é somente leitura.
- Transições de estado (`business-rules.md` §6).

### F8 — Frontend (RF01 a RF08, RNF05)

- Base:
  - `react-oidc-context` + `oidc-client-ts`, com renovação silenciosa do token.
  - `react-router` com rotas protegidas por papel.
  - Cliente HTTP único que injeta o token e interpreta Problem Details.
  - Leitor de SSE com `fetch` (P6).
  - `@tanstack/react-query`.
  - `env.js` para a configuração (P5).
- Telas:
  - **Catálogo** (admin e cliente-interno; o cliente externo não tem acesso): lista paginada e filtros. Só o Admin vê o formulário (`react-hook-form` + `zod`) com tipo, categoria, unidade, preço com 2 casas e fornecedor, e as ações de inativar e reativar.
  - Os dados dos materiais aparecem para todos os papéis na tela de cotação (itens e sugestões).
  - **Projetos:** lista, abertura (somente leitura se arquivado) e arquivamento.
  - **Cotação / chat:**
    - Descrição e resposta em streaming.
    - Tabela de itens com subtotais e total.
    - Lista destacada de itens não encontrados.
    - Botões para confirmar as `sugestoes` de material, que enviam a confirmação como mensagem.
    - Perguntas de esclarecimento.
    - Mensagem fixa em caso de falha (RN12).
- Moeda com `Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })` e quantidades com 2 casas, sempre exibindo os valores que a API devolve, sem recalcular no navegador.
- WCAG 2.1 AA (`aria-live` no chat, foco e teclado) e layout responsivo.
- Testes de componentes (Vitest + Testing Library) nos fluxos de cotação, confirmação e catálogo.

### F9 — Contêineres e segurança

- Dockerfiles *multi-stage*, com imagens base oficiais, tags fixas e usuário não-root.
- Nenhum segredo na imagem: tudo vem do `.env` local.
- Imagens `frontend` e `backend` geradas com tag semver e mantidas no Docker da máquina (sem registro).
- Segredos fora do ambiente local: AWS KMS. Fica fora do escopo enquanto só existir o ambiente local.
- Rotina de backup do volume do MongoDB (`mongodump`) documentada (ADR-009).
- Logs estruturados sem a chave da API e sem tokens.
- Revisão de prompt injection: a descrição nunca altera preço, porque o preço só vem do catálogo congelado.
- `dotnet list package --vulnerable` e `npm audit` sem itens críticos ou altos.

### F10 — RF10, gestão de usuários (opcional, Could)

- CRUD `/api/v1/usuarios` repassado à Keycloak Admin API (cliente `precificacao-admin`).
- Tela de usuários para o Admin, com criação de usuário e atribuição de papel (não há autocadastro).

### F11 — Suíte de regressão, aceite e release 1.0.0

- **Catálogo de referência:** materiais com preços de exemplo pesquisados na internet durante o desenvolvimento e fixados no repositório.
- **30 descrições de referência**, cada uma com a resposta fixa do *mock* do LLM e o valor esperado. Rodam como testes unitários (`standards.md` §6 e §7).
- Demonstração dos critérios de aceite ao PO.
- OpenAPI e guias de usuário revisados.
- Tag `v1.0.0` e imagens `1.0.0` geradas no Docker local.

## 5. Sequência e dependências

```mermaid
flowchart LR
    F1[F1 Fundação] --> F2[F2 Keycloak + Kong]
    F1 --> F3[F3 Base Backend]
    F2 --> F3
    F3 --> F4[F4 Catálogo]
    F3 --> F5[F5 Precificação]
    F4 --> F6[F6 MCP + Agente]
    F5 --> F6
    F6 --> F7[F7 Projetos + Conversas]
    F2 --> F8[F8 Frontend]
    F4 --> F8
    F7 --> F8
    F7 --> F9[F9 Contêineres + Segurança]
    F8 --> F9
    F9 --> F10[F10 Usuários - opcional]
    F9 --> F11[F11 Regressão + Release 1.0.0]
    F10 -.-> F11
```

Trabalhos que podem andar em paralelo:
- F4 e F5.
- A base do Frontend (login, rotas e catálogo) logo depois de F2 e F4.
- O catálogo e as descrições de referência da F11 a partir de F5.

## 6. Rastreabilidade

| Requisito / regra | Fase | Verificação |
| --- | --- | --- |
| RF01 Login OIDC | F2, F3, F8 | Login PKCE, `/usuarios/me`, `401` sem token |
| RF02 / RN10 Materiais | F4, F8 | Testes do `MaterialService` e dos validadores (duplicidade, 2 casas, reativação) |
| RF03 Cotação | F6, F7, F8 | Teste do agente e suíte de regressão |
| RF04 / RN02 Refinamento | F7 | Quantidade, inclusão, remoção e troca funcionam; preço de item já cotado não muda; pedido de alteração de preço é recusado |
| RF05 / RN03 Não encontrado | F6 | `ITENS_NAO_ENCONTRADOS` sem valor parcial |
| RF06 / RN02 Cotação congelada | F7 | Mudar o preço do catálogo não altera a cotação |
| RF07 Listar/abrir/excluir | F7, F8 | Visibilidade e arquivamento |
| RF08 / RN06 / RN09 Esclarecimento | F6, F8 | Medida faltando; confirmação entre 80% e 100% |
| RN01 Cálculo | F5 | Testes do Serviço de Precificação |
| RN04 Exclusão lógica | F4, F7 | Inativação, reativação e arquivamento |
| RN05 Unidades | F5 | Teste de cada linha da tabela |
| RN07 Visibilidade | F3, F7 | `404` para projeto de outro cliente |
| RN08 Precisão e arredondamento | F5 | Os 7 casos da tabela RN08; valores gravados com 2 casas |
| RN09 Similaridade | F6 | Levenshtein normalizado e limites de 80% e 100% |
| RN11 Arquivado | F7 | `422 PROJETO_ARQUIVADO` |
| RN12 Falha | F6 | `500 FALHA_PROCESSAMENTO` com a mensagem fixa |
| RNF01 Segurança | F2, F3, F9 | JWT no Kong e no Backend; segredos fora da imagem |
| RNF02 Desempenho | F4, F7 | p95 do CRUD; primeiro byte do SSE em até 3 s |
| RNF04 Manutenibilidade | Todas | Cobertura de 70% ou mais no `verificar.sh`; OpenAPI |
| RNF05 Usabilidade | F8 | Checklist WCAG 2.1 AA |

## 7. Riscos

| Risco | Impacto | Mitigação |
| --- | --- | --- |
| O Levenshtein compara letras, não significado: "tinta reflexiva" × "Película refletiva" dá 61%, e "placa" × "Chapa de aço galvanizado" dá 17% | Muitos itens não encontrados quando o cliente usa outras palavras | Nomes de materiais claros no catálogo; decidir Q9 |
| A suíte de regressão usa *mock* do LLM, então não mede o efeito real de mudanças no prompt | Uma regressão de prompt só aparece no uso real | Risco aceito no MVP |
| Sem CI, as verificações dependem de rodar o `verificar.sh` | Um PR pode entrar sem cobertura ou com aviso | DoD exige anexar a saída do `verificar.sh` ao PR |
| Chave fixa do realm (ADR-010) | Rotação manual | Script de geração e procedimento documentado |
| Kong OSS 3.9 sem novas linhas OSS | Fim das atualizações | Reavaliar em ADR quando sair do MVP |
| Custo e latência do Claude Opus 5.5 sem limite de uso | Gasto não controlado | Monitorar o uso; o limite pode entrar depois no Kong (`rate-limiting`) |
| Diferença de `issuer` entre a rede interna e a URL pública do Keycloak | Tokens recusados | `KC_HOSTNAME` + `MetadataAddress` interno + `ValidIssuer` público |

## 8. Pontos em aberto

Os pontos Q1 a Q8 foram respondidos e estão nos documentos `.ai` v0.7. Restam três pontos, que surgiram dessas respostas.

| ID | Ponto | Fase | Proposta do plano |
| --- | --- | --- | --- |
| **Q9** | O Levenshtein compara o termo com o nome do material letra a letra. Se o LLM usar as palavras do cliente (P10), sinônimos ficam abaixo de 80% e viram "não encontrado". Se o LLM receber a lista de nomes do catálogo, ele mesmo escolhe o nome exato, e a faixa de confirmação (80–99%) quase nunca acontece. Qual comportamento é o desejado? | F6 | Usar as palavras do cliente (P10), mantendo a confirmação como a RN09 descreve, e cadastrar nomes claros no catálogo |
| **Q10** | Com quantidade gravada em 2 casas e cálculo em 3, o cliente pode ver uma conta que não fecha. Exemplo: 0,17 h × R$ 85,00 aparece na tela, mas o subtotal é R$ 14,19 (calculado com 0,167 h), e não R$ 14,45. Isso é aceitável? | F5, F8 | Aceitar e mostrar uma nota na tela de cotação de que as quantidades exibidas estão arredondadas |
| **Q11** | O AWS KMS gerencia chaves de criptografia; ele não guarda segredos (senhas, chave da Claude API) por si só. Para guardar segredos, o comum é o AWS Secrets Manager ou o Parameter Store, que usam o KMS por baixo | F9 | Sem impacto agora, porque só existe o ambiente local com `.env`. Revisar quando houver outro ambiente |
