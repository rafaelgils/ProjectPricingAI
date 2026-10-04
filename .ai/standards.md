# Padrões de código e estilo

> Sistema de Cotação de Projetos · versão 0.9 · 04/10/2026
> Regras que todo código, API e prompt do projeto seguem. Exceções exigem ADR em `architecture.md`.

## 1. Controle de versão

- Fluxo **trunk-based**: branches curtas a partir de `main`, nomeadas `feature/<id>-<descricao>`, `fix/<id>-<descricao>` ou `docs/<descricao>`.
- Merge só por Pull Request, com **1 aprovação** e build, lint e testes passando localmente (não há pipeline de CI no MVP); estratégia *squash merge*.
- Mensagens no padrão **Conventional Commits** (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`).
- Releases com **versionamento semântico** (MAJOR.MINOR.PATCH) e tag no Git.

## 2. Princípios de design: SOLID e Clean Code

Valem para Backend e Frontend e são critério de aprovação em revisão de código.

**SOLID**

| Princípio | Como aplicar neste projeto |
| --- | --- |
| **S** — Responsabilidade única | Cada classe tem um motivo para mudar: o Serviço de Precificação só calcula, o repositório só persiste, o controller só traduz HTTP |
| **O** — Aberto/fechado | Novas regras (unidade de medida, forma de cálculo) entram como novas implementações, sem alterar o código estável |
| **L** — Substituição de Liskov | Qualquer implementação de uma interface (repositório, cliente do LLM) pode substituir outra sem quebrar quem a usa |
| **I** — Segregação de interfaces | Interfaces pequenas por necessidade (`IMaterialRepository`, `IProjetoRepository`), nunca um repositório genérico com tudo |
| **D** — Inversão de dependência | O domínio depende de abstrações; implementações são injetadas pelo container de DI do ASP.NET Core. Isso permite substituí-las por *mocks* nos testes |

**Clean Code**

- Nomes descritivos que revelam a intenção, usando os termos do domínio (`calcularSubtotal`, não `calc`).
- Funções curtas que fazem uma coisa só, com no máximo 3 parâmetros (acima disso, um objeto de parâmetros).
- Sem números mágicos: constantes nomeadas (ex.: o limite de arredondamento da RN08).
- Sem código morto ou comentado; comentários explicam o *porquê*, não o *o quê*.
- Erros por exceções de domínio com mensagem clara; nunca retornar `null` para sinalizar erro.
- Sem duplicação (DRY); regra comum vai para um único lugar.
- Regra do escoteiro: todo PR deixa o código tocado um pouco mais limpo do que encontrou.

## 3. Backend (.NET)

- Seguir as convenções de codificação C# da Microsoft; `.editorconfig` versionado na raiz.
- Analisadores do .NET ativos; avisos tratados como erro (`TreatWarningsAsErrors`).
- `Nullable` habilitado em todos os projetos.
- Termos de domínio em português, iguais ao modelo de dados (`Material`, `Projeto`, `ItemProjeto`, `Conversa`).
- Valores monetários e quantidades sempre `decimal` em C# e `Decimal128` no MongoDB. **Nunca** `double` ou `float`.
- Precisão conforme RN08 (`business-rules.md`): valores gravados e devolvidos pela API têm 2 casas decimais; os cálculos usam 3 casas. A quantidade é levada a 2 casas antes de multiplicar, para o cálculo usar a quantidade exibida.
- Arredondamento implementado em funções únicas do Serviço de Precificação:
    - Para 3 casas: `Math.Round(valor, 3, MidpointRounding.AwayFromZero)`.
    - De 3 para 2 casas: limiar próprio na 3ª casa decimal (constante `LimiarArredondamento = 6`). Vale para reais e quantidades. Não usar o `Math.Round` padrão nessa etapa, porque ele segue outra regra.
- Similaridade da RN09: distância de Levenshtein normalizada sobre os textos normalizados (minúsculas, sem acentos, sem espaços extras), comparando o termo com o nome e com cada sinônimo do material.
    - Implementada no próprio Backend, sem biblioteca externa, numa única função de domínio e de forma determinística.
    - O limiar é uma constante nomeada (`LimiarSimilaridade = 0.80`). O LLM não decide a similaridade.
- I/O assíncrono de ponta a ponta, propagando `CancellationToken`.
- Autorização por *policies* baseadas nos papéis do Keycloak (`admin`, `cliente-interno`, `cliente-externo`); nunca checar papel por string espalhada no código.
- Validação de entrada na borda da API (ex.: FluentValidation); regras de negócio ficam no domínio, não nos controllers.
- Acesso ao MongoDB só pela camada de repositório; tools MCP chamam serviços de domínio, nunca o banco direto.

## 4. Frontend (React)

- TypeScript em modo `strict`; proibido `any` sem comentário justificando.
- ESLint + Prettier obrigatórios, executados antes de cada Pull Request.
- Componentes funcionais com hooks; um componente por arquivo, em `PascalCase.tsx`.
- Chamadas HTTP centralizadas num cliente único que injeta o token; estado de servidor gerenciado por biblioteca de cache de consultas (ver `tech-stack.md`).
- Textos de interface em português; acessibilidade WCAG 2.1 AA (rótulos, contraste, navegação por teclado).
- Valores em reais formatados com `Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })`.

## 5. API REST

| Item | Padrão |
| --- | --- |
| Rotas | Recursos no plural, minúsculos, em português, versão no caminho: `/api/v1/materiais`. Sem verbos na rota |
| Corpo | JSON em `camelCase` |
| Datas | ISO 8601 com fuso (`2026-10-04T14:30:00-03:00`) |
| Moeda | Campo `moeda` explícito (`"BRL"`) em toda resposta com valor |
| Erros | RFC 7807 (Problem Details) + campo `code` estável em maiúsculas (ex.: `ITENS_NAO_ENCONTRADOS`) |
| Paginação | Parâmetros `pagina` e `tamanho`; resposta com `total` |
| Exclusão | Lógica (inativação/arquivamento); nada é apagado fisicamente pela API |
| Documentação | Especificação OpenAPI nativa do .NET 10, atualizada no mesmo PR da rota e publicada em `/openapi/v1.json`. Sem Swagger |
| Streaming | `POST /projetos` e `POST /projetos/{id}/mensagens` respondem em SSE (`text/event-stream`), com `data` em JSON (ver abaixo) |

Códigos de status usados:

| Código | Quando |
| --- | --- |
| 200 / 201 / 204 | Sucesso (201 com cabeçalho `Location`) |
| 400 | Campos inválidos |
| 401 / 403 | Sem token / papel sem permissão |
| 404 | Recurso inexistente ou de outro cliente |
| 409 | Conflito (ex.: material com nome duplicado) |
| 422 | Regra de negócio violada |
| 500 | Falha no agente ou no LLM, com a mensagem fixa da RN12 |

Códigos de erro (`code`). Toda resposta de erro da API tem `code`, inclusive as geradas pelo próprio ASP.NET (401, 403, 404), e os títulos ficam em português:

| `code` | Status | Origem |
| --- | --- | --- |
| `VALIDACAO` | 400 | Campos inválidos ou corpo malformado |
| `NAO_AUTENTICADO` | 401 | Token ausente, inválido, expirado ou sem `aud=precificacao-api` |
| `ACESSO_NEGADO` | 403 | Papel sem permissão |
| `RECURSO_NAO_ENCONTRADO` | 404 | Recurso inexistente ou de outro cliente (RN07) |
| `MATERIAL_DUPLICADO` | 409 | RN10, nome ou sinônimo repetido |
| `ITENS_NAO_ENCONTRADOS` | 422 | RN03 |
| `ESCLARECIMENTO_NECESSARIO` | 422 | RN06 e RN09 |
| `PROJETO_ARQUIVADO` | 422 | RN11, mensagem "Este projeto está inativo." |
| `MEDIDA_INVALIDA` | 422 | RN05: unidade que não converte para a do material, medida faltando ou quantidade que zera ao arredondar |
| `FALHA_PROCESSAMENTO` | 500 | RN12 |
| `ERRO_INTERNO` | 500 | Erro inesperado; o detalhe vai só para o log |

O `type` é derivado do `code` (ex.: `ITENS_NAO_ENCONTRADOS` → `https://precificacao/erros/itens-nao-encontrados`). Requisições sem token são barradas no Kong, que responde `401` no formato próprio dele (`{"message":"Unauthorized"}`).

**Respostas em SSE**

- Os erros encontrados **antes** de abrir o stream são respostas HTTP comuns em Problem Details: `400`, `401`, `403`, `404` e `422 PROJETO_ARQUIVADO`.
- Depois disso, `POST /projetos` responde `201` com `Location` e `POST /projetos/{id}/mensagens` responde `200`.
- Os resultados chegam em eventos, sempre com `data` em JSON:

| Evento | Conteúdo de `data` |
| --- | --- |
| `delta` | `{ "texto": "..." }`, com o texto parcial da resposta do agente |
| `cotacao` | O projeto com itens, `valorTotal` e `moeda` (`business-rules.md` §8) |
| `erro` | Problem Details com `status` e `code` (`ITENS_NAO_ENCONTRADOS`, `ESCLARECIMENTO_NECESSARIO` ou `FALHA_PROCESSAMENTO`) |
| `fim` | `{ "projetoId": "...", "status": "rascunho|cotado" }`; encerra o stream |

## 6. Agente, prompts e tools

- Prompts de sistema e definições das tools MCP ficam versionados no repositório, revisados em PR como código.
- O LLM **nunca** calcula valores: retorna itens, quantidades, medidas e unidades informadas em saída estruturada, validada por schema no Backend antes do cálculo.
- O LLM recebe só a tool `buscarMateriais`. As tools `calcular` e `salvarProjeto` são chamadas apenas pelo código do agente (ADR-006).
- Sugestão de material (RN09, passo 2): o LLM só escolhe entre os materiais da lista enviada pelo agente. O `materialId` devolvido é validado contra a lista, e a sugestão sempre passa por confirmação do cliente.
- **Suíte de regressão:** 30 descrições de referência com valor esperado, sobre um catálogo de referência. Os preços desse catálogo são valores de exemplo pesquisados na internet durante o desenvolvimento e fixados no repositório. A suíte é composta de testes unitários: o LLM é um *mock* que devolve uma resposta fixa por descrição (§7). Toda mudança de prompt, de schema ou do Serviço de Precificação roda a suíte. Como o LLM é simulado, a suíte não mede o efeito real de uma mudança de prompt; esse risco foi aceito no MVP.

## 7. Testes

- **Somente testes unitários**, com `xUnit` e **`Moq`**. Dependências externas (repositórios MongoDB, cliente do LLM, Keycloak Admin API) são substituídas por *mocks*; nenhum teste acessa banco real, chama a Claude API ou sobe contêiner.
- O *mock* do LLM devolve respostas fixas, definidas durante o desenvolvimento, inclusive na suíte de regressão (§6).
- Cobertura mínima de **70%** no Backend, com prioridade para o Serviço de Precificação (cálculo, conversão de unidades, arredondamento da RN08) e para as regras RN02, RN03 e RN09.
- Cobertura do Backend medida com `coverlet.collector` e a do Frontend com `@vitest/coverage-v8`.
- Frontend: testes de componentes nos fluxos de cotação e catálogo.
- Não há testes ponta a ponta no escopo do projeto.

## 8. Contêineres

- **Um contêiner por camada**, cada um com seu `Dockerfile` versionado no respectivo projeto:

| Camada | Contêiner | Conteúdo |
| --- | --- | --- |
| Frontend | `frontend` | Build estático do React servido por Nginx |
| Backend | `backend` | API .NET com Agente de Projetos, Servidor MCP e Serviço de Precificação |
| Banco | `mongodb` | Imagem oficial do MongoDB com script de inicialização (índices) e volume persistente para os dados |

- Keycloak e Kong rodam a partir das suas imagens oficiais, sem imagem própria.
- Imagens base apenas oficiais e com tag de versão fixa, nunca `latest`; a lista está em `tech-stack.md`.
- Imagens de Frontend e Backend construídas com *multi-stage build* (etapa de build separada da etapa de execução).
- Ambiente local sobe todas as camadas com um único `docker compose up`.
- Configuração por variáveis de ambiente; nenhuma chave ou senha gravada na imagem. No ambiente local, os segredos ficam num `.env` fora do Git; fora dele, no AWS Secrets Manager.
- O ambiente local usa HTTP. Neste momento só existe o ambiente local.
- Tags das imagens seguem a versão semântica da release. Por enquanto não há registro de imagens: as imagens ficam no Docker instalado na máquina.

## 9. Definition of Done

- Critérios de aceite atendidos e demonstrados ao PO.
- Código revisado segundo SOLID e Clean Code (seção 2).
- Testes unitários passando e cobertura dentro do mínimo.
- Sem vulnerabilidade crítica ou alta na análise de dependências (`dotnet list package --vulnerable` e `npm audit`).
- Imagem do contêiner gerada com sucesso (`docker compose build`).
- OpenAPI, guias de usuário e ADR (quando houver decisão) atualizados.

## 10. Documentação

- Decisões de arquitetura registradas como ADR numerado em `architecture.md`.
- Decisões de negócio registradas em `business-rules.md` §9.
