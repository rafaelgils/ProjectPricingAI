# Prompts utilizados — Refatoração

Entregável da atividade "Refatoração de Arquitetura". Os prompts seguem as três fases do roteiro da Aula 4
(`04.1_Guia_Demonstracao.md`), adaptados ao ProjectPricingAI: o roteiro é voltado a Java (Records, Virtual Threads), e este projeto já
é .NET 10, então os prompts de migração de versão foram trocados por prompts de arquitetura e segurança.

> **Nota de transparência.** Os prompts P0.1 e P0.2 são as mensagens do aluno, quase literais. Os prompts P1.1 a P3.2 descrevem,
> no formato do roteiro, a instrução dada em cada etapa da execução assistida; foram redigidos a partir do que foi feito e
> são representativos, não uma transcrição palavra a palavra da conversa.

## 0. Prompts de condução (mensagens do aluno)

**P0.1 — Enunciado e ideias do grupo**

> Atividade Prática — Refatoração de Arquitetura. Aplicar técnicas de "Modernização de Sistemas" no projeto das aulas anteriores.
> Identificar dívidas técnicas (inclusive geradas pela própria IA) e realizar uma refatoração estruturada, sem adicionar funcionalidades.
> Entregáveis: código na branch `refactoring`, prompts utilizados e relatório de modernização.
> Sugestão do grupo: o sistema está monolítico, sem camada de segurança, sem atenção à idempotência, e os acessos são de usuários de
> uma única empresa (refatorar para SaaS / tenants diferentes). Pedido: mais ideias de refatoração seguindo o roteiro da Aula 4 e um planejamento.
> Restrições: não atuar na `main` (criar branch), manter as edições no nome do aluno.

**P0.2 — Autonomia nas decisões**

> Sobre as decisões minhas e do grupo, tome decisões como um engenheiro sênior especialista. E pode seguir seu plano na branch apartada!

Decisões tomadas a partir de P0.2 (registradas nos ADRs): coleção compartilhada com `tenantId` e tenant em claim do JWT num único
realm (ADR-011); monolito modular em vez de extrair o agente (ADR-014); recorte "núcleo + destaques do grupo".

## 1. Fase de pré-refatoração (blindagem)

**P1.1 — Baseline de testes**

> Execute a suíte completa do backend do ProjectPricingAI e registre o baseline (quantidade de testes por projeto, tempo, linhas de
> código de produção e de teste, maiores arquivos, quantidade de `catch (Exception)`). Esse baseline é o *golden master*: nenhum teste
> existente pode mudar de comportamento durante a refatoração.

**P1.2 — Mapeamento de acoplamento e dívida técnica**

> Identifique *code smells* arquiteturais no ProjectPricingAI. Verifique como `AgenteProjetos`, `FerramentasCotacao` e os endpoints se
> acoplam ao domínio e à infraestrutura, se há vazamento de lógica de persistência, e se existe isolamento entre empresas, idempotência
> e proteção de uso. Rode os analisadores do .NET em modo máximo (`AnalysisMode=All`) e separe sinal de ruído. Liste os riscos de quebrar
> o sistema em cada refatoração proposta.

## 2. Fase de execução (transformação assistida)

**P2.1 — Multi-tenancy (ADR-011)**

> O sistema atende uma única empresa. Refatore para SaaS multi-tenant: o tenant deve vir de uma claim do JWT (nunca do cliente), o
> isolamento deve ficar num único ponto do código (repositório base) para que nenhum caso de uso possa esquecê-lo, os índices únicos
> devem passar a valer por tenant e a gestão de usuários do Keycloak deve ser restrita ao tenant do administrador. Compare as
> alternativas (banco por tenant, realm por tenant, coleção compartilhada), registre a decisão em ADR e escreva a migração dos dados
> existentes. Atualize os testes apenas onde a assinatura mudou e crie testes de isolamento entre tenants.

**P2.2 — Idempotência (ADR-012)**

> Torne idempotentes os dois POSTs que acionam o agente (criar projeto e enviar mensagem), cuja resposta é um stream SSE. Use o cabeçalho
> `Idempotency-Key`, escopo por tenant e usuário, e reenvie a resposta guardada em repetições sem chamar o LLM de novo. Defina o
> comportamento para chave em andamento, chave com conteúdo diferente e falhas transitórias. Atualize o frontend e o CORS do gateway.

**P2.3 — Camada de segurança (ADR-013)**

> Adicione uma camada de segurança sem mudar regras de negócio: limites de uso por IP (Kong), por usuário e por tenant (backend), com
> cota específica para chamadas que acionam o LLM e resposta 429 em Problem Details; defesa contra injeção de prompt (sanitizar tudo
> que é interpolado em prompts, limitar a saída do LLM, reforçar os prompts versionados); e recusa de segredos com valor de exemplo na
> subida. Verifique se a modernização não introduziu nova superfície de ataque.

**P2.4 — Desacoplamento de domínio e SRP (ADR-014)**

> A classe `AgenteProjetos` tem 374 linhas e mistura interpretação do LLM, validação de schema, correspondência com o catálogo e
> orquestração. Extraia colaboradores com contrato próprio, mantendo o comportamento idêntico: a suíte de regressão e os testes do
> agente não podem ter nenhuma asserção alterada.

**P2.5 — Fronteiras arquiteturais verificáveis (ADR-014)**

> Transforme a arquitetura em regra executável: escreva testes que impeçam o domínio de referenciar outras camadas, MongoDB, Anthropic
> ou ASP.NET, e que impeçam dependências indevidas entre os módulos Catálogo, Cotação e Identidade. Não adicione bibliotecas fora da
> lista de `tech-stack.md`. Inclua um teste que garanta que o detector não está vazio.

**P2.6 — Higiene, DRY e resiliência (ADR-015)**

> Corrija os achados reais dos analisadores (CA2000, CA1001), elimine a duplicação que a introdução do tenant criou nas entidades e
> adicione resiliência (timeout, retry com jitter e circuit breaker) ao cliente HTTP do Keycloak, sem repetir métodos não seguros.
> Carregue as rotas do frontend sob demanda para reduzir o pacote principal.

## 3. Fase de pós-refatoração (validação e relatório)

**P3.1 — Validação completa**

> Rode o script de verificação do projeto (build com avisos como erro, regressão, testes com cobertura mínima de 70 %, auditoria de
> dependências, lint, formatação e build do frontend) e confirme que todos os 375 testes originais continuam passando.

**P3.2 — Relatório de Modernização (dívida paga)**

> Gere o comparativo técnico "antes × depois" do ProjectPricingAI: diagnóstico (code smells, violações de padrão, obsolescência), e para
> cada problema o formato Problema → Técnica aplicada → Prompt → Validação. Inclua o que foi avaliado e **não** alterado, com motivo,
> e declare explicitamente o que não pôde ser validado no ambiente.
