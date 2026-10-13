# Relatório de Modernização — ProjectPricingAI

> Branch `refactoring` · base: `main` no commit `73ccef1` · escopo: auditoria técnica e modernização, sem novas funcionalidades de negócio.
> Plano de origem: [`plano-de-refatoracao.md`](plano-de-refatoracao.md) · Prompts: [`prompts.md`](prompts.md) · Decisões: ADR-011 a ADR-015 em [`.ai/architecture.md`](../../.ai/architecture.md).

## 1. Resumo executivo

O sistema (cotação de projetos em linguagem natural, com agente de LLM e cálculo determinístico) estava funcional e bem testado, mas
carregava quatro dívidas estruturais que o impediam de operar como produto SaaS: **atendia uma única empresa**, **não era idempotente**,
**não tinha camada de proteção de uso/custo** e **tinha o agente concentrado em uma classe de 374 linhas** sem fronteiras
verificáveis entre módulos.

| Resultado | Antes | Depois |
| --- | --- | --- |
| Testes do backend | 375 | **447** (+72), todos aprovados |
| Testes do frontend | 22 | **24** |
| Cobertura de linhas do backend | não registrada no baseline | **86 %** (mínimo exigido: 70 %) |
| Maior arquivo do backend | `AgenteProjetos.cs`, 374 linhas | `KeycloakAdminClient.cs`, 252; `AgenteProjetos.cs`, **146** |
| Isolamento entre empresas | nenhum (`clienteId` apenas) | filtro de `tenantId` num único ponto (repositório base) |
| POSTs que chamam o LLM idempotentes | 0 de 2 | **2 de 2** (opcional via `Idempotency-Key`) |
| Limites de uso | nenhum | 3 níveis (IP no Kong; usuário e tenant no backend) |
| Fronteiras entre camadas/módulos | convenção | **10 testes de arquitetura** |
| Pacote principal do frontend | 566 kB | **424 kB** (páginas em *lazy loading*) |
| Vulnerabilidades de dependências | 0 | 0 |

Os 375 testes originais continuam passando. As únicas alterações em testes existentes foram de **assinatura** (construtor dos
repositórios, do serviço de usuários e do agente) e a inclusão do `tenantId` nos filtros esperados — nenhuma regra de negócio
(cálculo, arredondamento, RN01–RN12) teve asserção alterada. A suíte de regressão com o catálogo de referência passou sem mudanças.

## 2. Diagnóstico

### 2.1 Code smells e violações de padrão

| # | Achado | Evidência | Gravidade |
| --- | --- | --- | --- |
| D1 | **Classe deus no agente**: interpretação do LLM, validação de schema, correspondência com o catálogo (RN09), sugestão e orquestração na mesma classe | `AgenteProjetos.cs` com 374 linhas, 7 responsabilidades | Média |
| D2 | **Sem multi-tenancy**: nenhuma entidade tem `tenantId`; catálogo global; índices únicos globais; usuários do Keycloak sem vínculo a empresa | busca por `tenant` no código: 0 ocorrências | Alta (bloqueia o modelo SaaS) |
| D3 | **Falta de idempotência** em `POST /projetos` e `POST /projetos/{id}/mensagens` | duplo clique ou *retry* cria projeto duplicado e repete a chamada paga ao LLM | Alta |
| D4 | **Sem proteção de uso e custo**: nenhum *rate limit*; o Kong só tinha `jwt` e `cors` | cada mensagem é uma chamada paga | Alta |
| D5 | **Entrada do cliente interpolada em prompts sem tratamento**: termos, nomes e categorias do catálogo entravam com quebras de linha | permite forjar seções ("## Itens atuais…") no prompt | Média |
| D6 | **Saída do LLM sem limites** (quantidade de itens, tamanho de termo, magnitude das medidas) | medidas absurdas chegavam ao cálculo | Média |
| D7 | **Segredos de exemplo aceitos**: `.env.example` traz `troque-esta-senha` e o backend subia com ele | senha pública em produção por esquecimento | Média |
| D8 | **Fronteiras só por convenção**: nada impedia o domínio de referenciar MongoDB, ou o catálogo de depender do agente | sem testes de arquitetura | Média |
| D9 | **Recursos não descartados**: `SemaphoreSlim` em tipo não descartável; `FormUrlEncodedContent` sem `using` | analisador CA1001 e CA2000 | Baixa |
| D10 | **Duplicação (DRY)** ao introduzir o tenant em 3 entidades | `TenantId` + `DefinirTenant` repetidos | Baixa (gerada durante a refatoração e corrigida na hora) |
| D11 | **Cliente HTTP do Keycloak sem resiliência** embora `Microsoft.Extensions.Http.Resilience` constasse na stack permitida | nenhum timeout/retry/circuit breaker | Média |
| D12 | **Pacote único do frontend** de 566 kB, com aviso do bundler | `vite build` | Baixa |

### 2.2 Obsolescência

Não há. O projeto já usa .NET 10 (LTS), React 19, TypeScript estrito, MongoDB 8, Keycloak 26 e `npm audit`/`dotnet list package --vulnerable`
sem achados. A "modernização" aqui é **arquitetural e de padrões**, não de versão — por isso o item "migração de versão e *records*"
do roteiro da Aula 4 foi adaptado: os DTOs de API já eram `record`, e as entidades de domínio são mantidas como classes por causa da
mutabilidade controlada (`Projeto`, `Material`) e do mapeamento do driver do MongoDB.

### 2.3 Itens avaliados e **não** alterados (com motivo)

| Item | Decisão |
| --- | --- |
| `catch (Exception)` em `AgenteProjetos` e `CondutorConversa` | **Mantidos.** São fronteiras de falha por desenho (RN12): convertem qualquer erro do LLM/agente na mensagem fixa e no evento SSE de erro, e registram o detalhe no log. Trocar por tipos específicos deixaria vazar erros do SDK para o cliente. |
| Entidades de domínio como `record` | Mantidas como classe (ver 2.2). |
| OpenTelemetry (traces e métricas) | **Não feito.** Exige pacotes fora da lista de `tech-stack.md`, o que pede aprovação do Tech Lead. Fica como próxima evolução. |
| Extrair o agente em um segundo serviço | **Não feito, de propósito** (ADR-014): sem necessidade medida de escala independente, seria complexidade especulativa (YAGNI). As costuras para extrair no futuro foram preparadas. |
| Ruído dos analisadores `CA2007`, `CA1062`, `CA1515`, `CA1032` | Não aplicados: `ConfigureAwait` é irrelevante em ASP.NET Core, e o *nullable* estrito já cobre as checagens de nulo. |

## 3. Problemas, técnicas e prompts

Cada item abaixo segue o formato **Problema → Técnica aplicada → Prompt de refatoração → Validação**. Os textos completos dos prompts
estão em [`prompts.md`](prompts.md).

### 3.1 Blindagem (Fase 0)

- **Problema:** refatorar sem rede de segurança. **Técnica:** *test snapshotting* — rodar a suíte inteira e congelar o baseline
  (375 testes) antes de qualquer mudança; usar a suíte de regressão com o catálogo de referência como *golden master*.
  **Validação:** [`baseline.md`](baseline.md).

### 3.2 Multi-tenancy (D2) — ADR-011

- **Problema:** o sistema só atendia uma empresa; para virar SaaS cada empresa precisa de catálogo, usuários e projetos isolados.
- **Técnica aplicada:** *shared collection with discriminator*. `ITenantAtual` lê a claim `tenant_id` do JWT; o filtro do tenant fica
  **apenas** em `RepositorioMongo` (consultas, substituições e inserção), então nenhum caso de uso precisa lembrar dele. Todas as
  policies exigem a claim. Índices únicos viram compostos. A gestão de usuários consulta o Keycloak por atributo (`q=tenant_id:…`) e trata
  usuário de outra empresa como inexistente. Migração idempotente `001-multitenant.js` para bancos existentes.
- **Decisão sênior:** coleção compartilhada num único realm (e não banco ou realm por tenant) — menor custo operacional e sem
  duplicar a chave fixa do Kong (ADR-010). Registrado como alternativa rejeitada no ADR.
- **Validação:** testes de isolamento dos repositórios, do serviço de usuários e das policies (usuário sem tenant é negado mesmo
  sendo admin).

### 3.3 Idempotência (D3) — ADR-012

- **Problema:** repetição de `POST` duplicava projetos e custo de LLM.
- **Técnica aplicada:** *idempotency key* por filtro de endpoint. Guarda a resposta SSE inteira; repetição recebe o mesmo corpo com
  `Idempotent-Replayed: true`; chave em uso devolve `409`; chave com outro conteúdo devolve `422`. Falhas transitórias liberam a
  chave para nova tentativa. Escopo por tenant + usuário; TTL de 24 h. O frontend gera um UUID por ação e o reaproveita após queda de rede.
- **Achado durante os testes:** um teste de impressão digital passou "em falso" (todas as chaves geravam o mesmo resumo) porque o
  teste não entregava o corpo ao filtro; foi corrigido e o filtro foi confirmado.
- **Descoberto na integração:** o `Idempotency-Key` precisou entrar na lista de cabeçalhos permitidos do CORS do Kong, senão o navegador
  bloquearia o *preflight*.

### 3.4 Camada de segurança (D4, D5, D6, D7) — ADR-013

- **Técnica aplicada:** (1) *rate limiting* em duas camadas (Kong por IP; backend encadeado por usuário e por tenant, com cota
  específica para chamadas ao agente); (2) defesa em profundidade de prompt: sanitização de toda interpolação (`SegurancaDoPrompt.Linha`),
  limites da saída do LLM e instruções nos prompts versionados de tratar texto como dado; (3) `ValidadorDeSegredos` recusa valores
  de exemplo na subida.
- **O que já era sólido e foi preservado:** o LLM só tem a tool `buscarMateriais`; cálculo e gravação são do código; `materialId`
  sugerido é validado contra a lista enviada. Essa é a barreira final contra injeção de prompt.
- **Limitação conhecida:** os limites do backend são por processo (memória); com várias réplicas, o teto efetivo multiplica.

### 3.5 Desacoplamento do agente (D1) — ADR-014

- **Técnica aplicada:** *Extract Class* / SRP. `AgenteProjetos` virou orquestrador (146 linhas) sobre `IInterpretadorDeDescricao`
  e `IResolvedorDeMateriais`; JSON e registro de sugestões foram para classes internas compartilhadas.
- **Validação:** os 17 testes originais do agente e a suíte de regressão passaram **sem alterar asserções** — só a construção do agente nos
  dois pontos de teste mudou.

### 3.6 Fronteiras verificadas (D8) — ADR-014

- **Técnica aplicada:** testes de arquitetura por reflexão, sem biblioteca nova (respeitando a regra de `tech-stack.md`). Verificam a
  regra de dependência entre camadas e as fronteiras entre os módulos Catálogo, Cotação e Identidade. Há um teste de guarda contra
  testes vazios.

### 3.7 Higiene de código (D9, D10, D11, D12)

- `KeycloakAdminClient` implementa `IDisposable` e descarta `FormUrlEncodedContent` (CA1001, CA2000).
- `EntidadeDoTenant` elimina a duplicação criada ao introduzir o tenant (DRY).
- Pipeline de resiliência no cliente do Keycloak, com retry desligado para métodos não seguros (ADR-015).
- Rotas do frontend em *lazy loading*: pacote principal de 566 kB para 424 kB e fim do aviso do bundler.

## 4. Antes × depois (código)

| Métrica | Antes | Depois | Observação |
| --- | --- | --- | --- |
| Linhas de produção (backend) | 4.446 | 5.213 | +767: novas capacidades (tenant, idempotência, limites, segurança) |
| Linhas de testes (backend) | 4.179 | 4.993 | +814 |
| Linhas do frontend | 2.916 | 2.961 | +45 |
| Testes (backend + frontend) | 397 | 471 | +74 |
| `AgenteProjetos.cs` | 374 | 146 | −61 % |
| `catch (Exception)` genéricos | 3 | 3 | mantidos por desenho (2.3) |
| ADRs | 10 | 15 | ADR-011 a ADR-015 |

Esta refatoração **não remove boilerplate** (o projeto já era enxuto): o ganho está em **robustez e sustentabilidade**, e o código de
produção cresceu porque a modernização arquitetural adicionou capacidades (isolamento, idempotência, proteção). A legibilidade e a
manutenibilidade melhoram onde há redução: o agente, antes monolítico, é agora três classes coesas e testáveis isoladamente.

## 5. Como validar

```bash
./scripts/verificar.sh --sem-docker   # build, regressão, testes + cobertura, auditoria de dependências, lint e build do frontend
```

Resultado nesta branch: **todas as verificações passaram** (backend 86 % de cobertura; frontend 84 % de linhas; 0 vulnerabilidades).

### O que NÃO foi validado nesta sessão

O ambiente de desenvolvimento não tinha o Docker em execução, então **não foram executados**: a importação do realm do Keycloak
(atributo `tenant_id`, mapper da claim, perfil de usuário declarativo), os scripts do MongoDB (`01-colecoes-indices.js` e a migração
`001-multitenant.js`, validados apenas quanto à sintaxe) e o `docker compose build`. Antes do merge, rodar o ambiente completo e conferir:

1. Login com `admin.teste` e inspeção do token: deve conter `tenant_id = empresa-demo`.
2. Criar usuário pela tela de usuários e conferir o atributo no console do Keycloak.
3. Duas empresas: criar usuários com tenants diferentes e confirmar que não enxergam catálogo, projetos nem usuários um do outro.
4. Repetir um `POST /projetos` com o mesmo `Idempotency-Key` e conferir `Idempotent-Replayed: true`, sem nova chamada ao LLM.
5. Em banco existente: `./scripts/backup-mongodb.sh` e depois `./scripts/migrar-para-multitenant.sh`.

## 6. Riscos e pontos de atenção

- **Migração de dados e do realm:** bancos e realms já em uso precisam da migração e da recriação do contêiner do Keycloak
  (o realm só é importado na criação). Usuários existentes **sem** o atributo `tenant_id` passam a ser negados (`403`) até serem
  associados a uma empresa. Ver [`migracao.md`](migracao.md).
- **Sem administrador de plataforma:** não há perfil que enxergue vários tenants; é uma evolução natural.
- **Idempotência opcional:** clientes que não enviam o cabeçalho continuam sem proteção.
- **`.env` com valores de exemplo agora impede a subida do backend** (intencional).

## 7. Próximos passos recomendados

1. Executar o roteiro da seção 5 em ambiente com Docker e registrar o resultado.
2. Contador distribuído para os limites de uso (necessário com mais de uma réplica).
3. OpenTelemetry com `correlationId` do navegador até a chamada ao LLM (aprovar os pacotes).
4. Trilha de auditoria (alteração de preço, arquivamento) com *outbox*.
5. Perfil de administrador da plataforma e gestão de tenants.
