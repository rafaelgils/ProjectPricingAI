# Prompts — Arquitetura e geração de contexto

> Projeto: Engenharia Software 2.0 · 04/10/2026 · Ferramenta: **Claude** (chat)
> Contém os prompts solicitados no trabalho — criação da arquitetura e geração de contexto (estrutura `.ai/`) — e os prompts de ajuste fino usados para validar e refinar o resultado. As respostas completas do assistente não estão incluídas.

---

## 1. Prompt de criação da arquitetura

Enviado ao Claude junto com o **diagrama inicial** desenhado à mão para o trabalho:

![Diagrama inicial da arquitetura](Projeto-AI-Eng-MBA.png)

> Atue como um Project manager especializado em documentações.
>
> Com base na imagem anexada (diagrama de arquitetura com Frontend SPA, API Gateway, Backend .NET, Keycloak, Agente de Projetos, LLM, Tools via MCP, MongoDB e entidades de Material, Projetos e Histórico de chat), revise o diagrama presente, crie diagramas de sequência, documentação de sistema (requisitos, arquitetura, design), documentação do usuário (tutoriais, guias, manuais) e documentação de processo (normas, planos de projetos, relatórios) para dar início à implantação do sistema.

_O que foi feito:_ a partir do diagrama inicial ([`Projeto-AI-Eng-MBA.png`](Projeto-AI-Eng-MBA.png)), foi produzida a arquitetura do MVP — funcionalidades principais, tipos de usuário e permissões, revisão do diagrama de arquitetura (camadas frontend, backend e banco), entidades e relacionamentos, endpoints da API, tecnologias sugeridas, diagramas de sequência e os fluxos principais.

---

## 2. Ajuste fino — regras de cotação e remoção da busca web

> Faça os seguintes ajustes:
> - Item 8: cotação não deve mudar com alteração de preço de material.
> - Remover busca web, revendo todos os pontos onde a pesquisa aparece, para retornar um erro caso não encontre o material na base.

_O que foi feito:_ introduzida a cotação congelada (snapshot de nome e preço de cada item) e removida a busca web — item fora do catálogo passa a retornar erro, com todos os pontos da documentação e os diagramas revisados.

---

## 3. Prompt de geração de contexto

> Separe a documentação nos seguintes markdowns:
> - `standards.md` — convenções de código e estilo
> - `architecture.md` — decisões de alto nível (ADRs)
> - `tech-stack.md` — versões e libs permitidas
> - `business-rules.md` — lógica de negócio e domínio

_O que foi feito:_ a documentação da arquitetura foi estruturada nos quatro arquivos de contexto (`.ai/`) que depois serviram de base para a implementação por agentes de IA, com as versões de tecnologia confirmadas na web.

---

## 4. Ajuste fino — escopo, padrões e decisões de tecnologia

> Standands Correção:
> Remover menção a Textil, a aplicacao é voltado para cotação de projetos em geral, podendo ser de qualquer ramo
> Incluir pattern SOLID e Clean Code
> Observabilidade, gerenciamento de custo e rate limite do LLM remover do escopo e revisar todos os documentos que pode está mencionado
> Nos testes unitarios vão utilizer MOQ e para remover a integracao real com MongoDB em container nos testes.
> Remover testes ponta a ponta
> Utilizar container por camada (front, back e banco) se ja nao estiver referenciado em algum outro markdown
> Remover o item 8 segurança e LGPD
>
> Archicture Correção:
> No item Atributos de qualidade remover o RNF02 e RNF06 e RNF05 na sua totalidade
> Remover ADR-008 da doocmentacao
> ADR-009, usar o Antropic Opus 5.5
>
> Tech Stack:
> API Gateway usar a versão open source do Kong Gateway
> LLM Antropic Opus 5.5
> 2. Bibliotecas permitidas - Remover linha de Observilidade OpenTelemetry
> 4. Testes e qualidade - Remover linha do teste Ponta a Ponta, nao sera necessario
>
> Business Rules Correção:
> Remover menção a Textil, a aplicacao é voltado para cotação de projetos em geral, podendo ser de qualquer ramo como por exemplo Textil, revise se a menção em outras partes
> item 6: ajustar o grafico que está com itens um em cima do outro, nao legivel
> item 9: Questoes de negócios em aberto: - Não, apenas custos de Materiais. - Use tres casas decimais, se a Terceira for maior ou igual a 6 para cima e menor que 6 pra baixo. - Não Aplicado. Nao sera tratado no escopo desse projeto

_O que foi feito:_ os quatro arquivos de contexto foram ajustados — sistema renomeado para "Sistema de Cotação de Projetos", SOLID/Clean Code incluídos, Kong OSS e Claude Opus 5.5 adotados, escopo de observabilidade/E2E/segurança reduzido, e as regras de arredondamento e de cálculo acertadas.

---

## 5. Ajuste fino — propagar o uso de contêineres

> Reflita o uso de contêineres do documento `standards` nos outros arquivos quando necessário (por exemplo, adicionar Docker no tech stack).

_O que foi feito:_ Docker e Docker Compose entraram no `tech-stack.md` (com as imagens base por contêiner), a visão de implantação e o ADR de contêiner por camada entraram no `architecture.md`, e os demais documentos foram alinhados.
