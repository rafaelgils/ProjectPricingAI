# Plano de Refatoração — ProjectPricingAI

> Branch: `refactoring` (criada a partir de `main`). Objetivo da atividade: auditoria técnica e modernização, **sem novas funcionalidades de negócio**. Estrutura baseada no roteiro da Aula 4 (Blindagem → Transformação → Validação).

## 1. Ideias do grupo (ponto de partida)

| Ideia | Situação encontrada no código |
| --- | --- |
| Monolito | Um único processo `backend` concentra API, agente, MCP, precificação e gestão de usuários. Só há separação lógica em camadas (Dominio/Aplicacao/Infraestrutura/Api). |
| Sem camada de segurança | JWT é validado no Kong e no backend, mas não há *rate limiting*, limite de tamanho de entrada, proteção contra *prompt injection* nem trilha de auditoria. O `kong.yml` só tem os plugins `jwt` e `cors`. |
| Idempotência | Não existe nenhuma chave de idempotência. Um `POST /projetos` repetido (duplo clique, retry de rede, reconexão do SSE) cria projetos duplicados e **gasta chamadas pagas à Claude API**. |
| Multi-tenant (SaaS) | Não existe `tenantId`. Projetos são isolados só por `clienteId`; o catálogo de materiais é global; o realm do Keycloak é único. |

## 2. Princípio de escopo

Refatorar = mudar a estrutura **sem mudar o comportamento observável**. Por isso:

- Tudo o que mudar comportamento (tenant, idempotência, rate limit) entra como **"modernização arquitetural"**, com decisão registrada em ADR, e não como refatoração pura.
- O que for refatoração pura (DRY, SOLID, dead code, records, exceções) é protegido pelos testes existentes (~4,2 mil linhas) + suíte de regressão `casos-referencia.json`.
- O projeto já está em .NET 10 / React 19 / Mongo 8, portanto **não há migração de versão**. A "modernização" aqui é arquitetural e de padrões de código.

## 3. Fase 0 — Blindagem (antes de mexer em código)

| # | Ação | Entregável |
| --- | --- | --- |
| 0.1 | Rodar `scripts/verificar.sh` e registrar baseline (testes passando, cobertura, tempo de build). | `docs/refactoring/baseline.md` |
| 0.2 | Medir métricas "antes": linhas por projeto, maiores arquivos, complexidade ciclomática, nº de `catch (Exception)`, avisos do analisador. | tabela "Antes" do relatório |
| 0.3 | **Testes de caracterização** para lacunas: fluxo de `CondutorConversa`/SSE, concorrência otimista, isolamento por `clienteId`, regras de arredondamento (RN08). | novos testes verdes **antes** das mudanças |
| 0.4 | **Auditoria de dívida técnica** (prompt de mapeamento de acoplamento) gerando a lista priorizada. | `docs/refactoring/auditoria.md` |

Prompt-base (adaptado do roteiro): *"Identifique code smells arquiteturais no ProjectPricingAI. Verifique como `AgenteProjetos`, `FerramentasCotacao` e os endpoints se acoplam ao domínio e à infraestrutura. Liste os riscos de quebrar o sistema em cada refatoração proposta."*

## 4. Fase 1 — Transformação (itens priorizados)

Legenda: **Tipo** R = refatoração pura · M = modernização arquitetural (muda comportamento, exige ADR) · Esforço P/M/G.

### 4.1 Eixo Arquitetura

| ID | Item | Tipo | Esforço | Justificativa / técnica |
| --- | --- | --- | --- | --- |
| A1 | **Multi-tenancy (SaaS)**: `TenantId` em `Material`, `Projeto`, `Conversa`; resolução do tenant por claim do JWT; filtro global nos repositórios (`RepositorioMongo`); índices únicos de `materiais.nome/sinonimos` passam a ser compostos com `tenantId`. Estratégia de isolamento: coleção compartilhada com discriminador (decisão em ADR-011, comparando com banco por tenant e realm por tenant). | M | G | Atende a sugestão do grupo. Isolamento forçado na camada de repositório evita vazamento entre tenants por esquecimento. |
| A2 | **Idempotência**: cabeçalho `Idempotency-Key` em `POST /projetos` e `POST /projetos/{id}/mensagens`; coleção `chavesIdempotencia` com TTL; resposta repetida devolve o resultado salvo sem chamar o LLM de novo. | M | M | Evita duplicidade e custo de LLM. Já existe `Versao` (concorrência otimista) para reaproveitar. |
| A3 | **Monolito → monolito modular** com fronteiras explícitas: módulos `Catalogo`, `Cotacao`, `Identidade`, cada um com contrato público e testes de arquitetura (NetArchTest/ArchUnitNET) proibindo dependência cruzada. | R | G | Passo realista para o "monolito": modularizar primeiro e deixar a extração do agente em serviço como opção documentada em ADR, em vez de microsserviços prematuros (YAGNI). |
| A4 | **Desacoplar o agente do provedor de LLM**: extrair `AgenteProjetos` (374 linhas, maior arquivo) em colaboradores pequenos: `InterpretadorDescricao`, `ClassificadorCorrespondencias`, `SugestorMaterial`, `MontadorResposta`. | R | M | SRP/SOLID; torna cada etapa testável sem mock do `IChatClient` inteiro. |
| A5 | **Domain Services puros**: garantir que regra (RN03, RN05, RN09) não dependa de repositório; `FerramentasCotacao` vira só orquestrador. | R | M | Equivale ao prompt "Refatoração de Domínio (Desacoplamento)" do roteiro. |
| A6 | **Eventos de domínio / outbox** para auditoria (projeto cotado, material alterado). | M | M | Base da trilha de auditoria (S5) sem acoplar ao fluxo principal. Opcional. |

### 4.2 Eixo Segurança

| ID | Item | Tipo | Esforço | Justificativa |
| --- | --- | --- | --- | --- |
| S1 | **Rate limiting** por usuário/tenant no backend (`AddRateLimiter`) e no Kong (`rate-limiting`), mais cota diária de chamadas ao LLM por tenant. | M | P | Protege custo da Claude API (hoje ilimitado). |
| S2 | **Defesa contra prompt injection**: delimitar a entrada do cliente no prompt, limitar tamanho da descrição, validar que `materialId` e termos vindos do LLM pertencem ao tenant (já há validação contra a lista; estender). Testes com payloads maliciosos. | M | M | Risco específico de sistemas com LLM; relevante para o relatório. |
| S3 | **Cabeçalhos e CORS**: revisar a lista de origens do Kong, CSP no Nginx do frontend, `HSTS` condicionado a HTTPS. | R | P | Hardening barato. |
| S4 | **Segredos**: remover padrões "troque-esta-senha" funcionais, validar na subida que nenhum segredo está com valor de exemplo; preparar abstração `ISegredos` para o AWS Secrets Manager previsto no `tech-stack.md`. | R | P | Dívida declarada na própria documentação. |
| S5 | **Trilha de auditoria** (quem alterou preço de material, quem arquivou projeto). | M | M | Exigência típica de SaaS; combina com A6. |
| S6 | **Auditoria de dependências**: `dotnet list package --vulnerable` e `npm audit` no `verificar.sh`. | R | P | Fecha o item de "vulnerabilidades" do roteiro. |
| S7 | **Autorização por recurso** (verificar `tenantId` + `clienteId` num único ponto, não espalhado nos endpoints). | R | M | Elimina risco de IDOR. |

### 4.3 Eixo Qualidade de código (técnicas do roteiro)

| ID | Item | Técnica |
| --- | --- | --- |
| Q1 | Trocar `catch (Exception)` genéricos (`AgenteProjetos.cs:83`, `CondutorConversa.cs:49`) por hierarquia semântica e tratamento centralizado (`IExceptionHandler`/Problem Details já existe em `TratadorDeExcecoes`; consolidar). | Tratamento de exceções |
| Q2 | Eliminar duplicação (DRY) entre `MaterialRepository`, `ProjetoRepository` e `ConversaRepository` e entre validadores. | DRY |
| Q3 | Converter DTOs/requisições/respostas e *value objects* em `record` imutáveis; usar `required`/`init`; coleções `IReadOnlyList`/*collection expressions* C# 14. | Modernização sintática |
| Q4 | Substituir *magic strings* de códigos de erro e nomes de coleções por constantes/enums; remover código morto (usar `dotnet format` + analisadores IDE0051/IDE0052/CA1822). | Dead code / KISS |
| Q5 | Habilitar `Nullable` estrito + `TreatWarningsAsErrors` + `AnalysisMode=All` e zerar os avisos. | Padronização |
| Q6 | Observabilidade: OpenTelemetry (traces + métricas), `correlationId` do front ao LLM, métricas de tokens e latência por etapa do agente. | Modernização |
| Q7 | Frontend: separar componentes grandes (`PaginaCotacao`, `useConversa`), tipar eventos SSE com *discriminated unions* e `zod`, tratamento de erro único. | SRP / tipagem |
| Q8 | Resiliência no cliente do LLM: *retry* com *jitter*, *circuit breaker* e *timeout* via `Microsoft.Extensions.Http.Resilience` (já na stack). | Resiliência |

## 5. Fase 2 — Validação e evolução

| # | Ação |
| --- | --- |
| 5.1 | Reexecutar `verificar.sh` e a suíte de regressão; **nenhum teste de caracterização pode ser alterado** sem justificativa escrita. |
| 5.2 | Testes novos por item: isolamento entre tenants (A1), repetição da mesma chave (A2), 429 (S1), payloads de injeção (S2), testes de arquitetura (A3). |
| 5.3 | Revisão de segurança dos pontos novos (prompt de auditoria de injeção e IDOR). |
| 5.4 | Coletar métricas "depois" e gerar o **Relatório de Modernização — Dívida Paga** (antes × depois). |

## 6. Ordem sugerida e recorte

Não é necessário fazer tudo. Recomendação de recorte para a entrega, equilibrando valor e risco:

1. **Núcleo (obrigatório):** Fase 0 completa, Q1, Q2, Q3, Q4, Q5, A4, A5.
2. **Destaques que o grupo pediu:** A1 (tenant), A2 (idempotência), S1 + S2 (camada de segurança), A3 (modularização em vez de microsserviços).
3. **Se sobrar tempo:** S4, S5/A6, Q6, Q8, Q7.

Cada item = um commit (ou PR pequeno) com mensagem `refactor(<área>): ...`, testes verdes antes e depois.

## 7. Riscos e cuidados

- **A1 é o item mais invasivo:** mexe em índices do Mongo, Keycloak (claim `tenantId` por usuário/organização) e em todos os repositórios. Fazer com migração de dados idempotente e tenant padrão para dados existentes.
- A resposta SSE e o agente dependem da Claude API: **nenhum teste novo deve chamar a API real** (manter mock do `IChatClient`).
- Mudanças de comportamento (M) precisam constar no relatório como *modernização*, não como refatoração pura, para manter a honestidade metodológica.
- Registrar **todos os prompts** usados em `docs/refactoring/prompts.md` (entregável da atividade).

## 8. Entregáveis finais

| Entregável | Local |
| --- | --- |
| Código refatorado | branch `refactoring` |
| Prompts utilizados | `docs/refactoring/prompts.md` |
| Relatório de Modernização (Diagnóstico → Problema / Técnica / Prompt, mais antes × depois) | `docs/refactoring/relatorio-modernizacao.md` |
| ADRs novas (tenant, idempotência, modularização) | `.ai/architecture.md` |

## 9. Decisões pendentes do grupo

1. Estratégia de tenant: coleção compartilhada com `tenantId` (recomendada) ou banco por tenant?
2. O `tenantId` virá de claim no Keycloak (atributo/organização) em um único realm?
3. Entregar A3 como monolito modular (recomendado) ou extrair o agente em um segundo serviço?
