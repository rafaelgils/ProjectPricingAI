# Prompt — Implementação do sistema

> Projeto: Engenharia Software 2.0 · 04/10/2026 · Ferramenta: Claude Code (agente de IA na IDE), modelo Claude Opus 5.5
> Contém o prompt de **implementação** solicitado no trabalho — executável por agentes e seguindo os arquivos de contexto da pasta `.ai/` — e os prompts de ajuste fino usados para resolver dúvidas e refinar o resultado. As respostas completas do assistente não estão incluídas.

---

## 1. Prompt de implementação

> Crie um plano de execução para a implantação de um sistema de cotação de projetos. Utilize os arquivos encontrados na pasta `.ai` como regras de arquitetura, business, padrões e stack de tecnologia. Crie o plano tendo certeza da implantação; caso tenha dúvida, ao final da leitura, retorne todas as dúvidas a serem resolvidas.

_O que foi feito:_ o agente leu os quatro arquivos de contexto (`architecture.md`, `business-rules.md`, `standards.md`, `tech-stack.md`), gerou um plano de execução em fases (F1 a F11) e levantou um conjunto de dúvidas bloqueantes a serem decididas antes de implementar.

---

## 2. Ajuste fino — primeira rodada de decisões (dúvidas D1–D25)

> respostas:
>
> d1 - arredondado
>
> d2 - serviço de precificação
>
> d3 - apenas o código do agente
>
> d4 - Formato da Mensagem SSE com JSON
>
> d5 - 422
>
> d6 - Muda a quantidade, e o valor da precificação, mas não o valor do material. quebrar o campo cotadoEm, em dois campos: "criadoEm" e "alteradoEm". O cliente pode editar apenas quantidade de material usado, não valor do material, e apenas através do agente.
>
> d7 - Sim
>
> d8 - para ambiente local, apenas http
>
> d9 - .net 10 utiliza openAPI, não necessita swagger
>
> d10 - fará uma comparação do termo descrito com a base, caso haja uma incerteza de até 20%, o agente perguntará ao usuário o termo mais proximo para confirmar qual deve ser usado.
>
> d11 - Sim, Sim, Sim
>
> d12 - Pode incluir tipo. Texto livre. Sim. 2.
>
> d13 - Realm role. Não.
>
> d14 - Somente leitura. 422, com mensagem "Este projeto está inativo."
>
> d15 - Codigo 500, mensagem "Não foi possível processar essa mensagem no momento, favor contate o administrador"
>
> d16 - Atualmente não
>
> d17 - No mock da LLM, os valores serão valores fixos definidos no momento do desenvolvimento da aplicação, buscando valores exemplos da internet
>
> d18 - Não se preocupar no momento com CI. Docker para registro de imagens e KeyVault. No momento não se preocupar com ambientes de homologação e produção.
>
> d19 -
>
> d20 - Remover RF09 do MVP
>
> d21 - Admin também pode cotar
>
> d22 - Pode confirmar
>
> d23 - O desenvolvimento será via IA, não se preocupar com tamanho de equipe ou prazo.
>
> d24 - ignorar o extra.md
>
> d25 - como a entrega é mvp, ignorar/remover o rnf03
>
> favor ajustar a documentação de acordo com o conteudo das duvidas e respostas, sem criar novos documentos.
>
> Após os ajustes, releia os documentos e faça uma revisão do plano de execução.

_O que foi feito:_ as decisões foram aplicadas nos quatro arquivos de contexto e no plano; o agente releu tudo, revisou o plano contra os documentos e levantou novas dúvidas (Q1–Q8).

---

## 3. Ajuste fino — segunda rodada de decisões (Q1–Q8)

> q1: Levenshtein normalizado
>
> q2: correto
>
> q3: é possivel incluir, remover ou trocar. a unica coisa que não se pode fazer é alterar o preço do material sendo usado.
>
> q4: adicione as ferramentas sugeridas no documento
>
> q5: para dados visiveis e em base, 2 casa decimais, para calculo, utilizar 3 e arredondar para cima quando a terceira for maior que 5, para baixo caso for 5 ou menor
>
> q6: Sim
>
> q7: o cliente pode ver dados de materiais dentro da pagina de cotação. ele apenas não tem acesso à tela de crud dos materiais. Ele não pode adicionar, alterar ou remover materiais da base de materiais.
>
> Q8: por enquanto será usado apensa docker instalado na maquina, e KMS da amazon
>
> novamente reflita o esclarecimento nos documentos.

_O que foi feito:_ aplicadas as regras de similaridade (Levenshtein normalizado), de arredondamento e de permissões; incluídas as ferramentas de teste; e registradas as decisões de infraestrutura. Surgiram então os pontos Q9–Q11.

---

## 4. Ajuste fino — terceira rodada de decisões (Q9–Q11)

> aplique a sugestão do q9 e q10 nos documentos, quanto ao q11, utilize aws secrets manager e reflita o uso nos documentos

_Contexto das sugestões referenciadas:_ **Q9** — cadastrar sinônimos no material e, quando não houver correspondência, deixar o LLM sugerir um material da lista de ativos, sempre com confirmação do cliente; **Q10** — arredondar a quantidade para 2 casas antes de multiplicar, para o cálculo bater com o que aparece na tela; **Q11** — usar AWS Secrets Manager para os segredos fora do ambiente local.

_O que foi feito:_ a RN09 ganhou os dois passos (similaridade por nome/sinônimos e sugestão do agente), o cálculo passou a usar a quantidade com 2 casas, e os segredos fora do local passaram para o AWS Secrets Manager — refletido nos quatro documentos e no plano, que ficou sem pontos em aberto.

---

## 5. Início da implementação (execução por fases)

> Inicie o plano de implantação do sistema.

_O que foi feito:_ a implementação começou pela F1 (fundação do repositório) e seguiu pelas fases seguintes, construindo o MVP — backend .NET (domínio, aplicação, infraestrutura e API), frontend React, infraestrutura com Docker Compose (Keycloak, Kong e MongoDB) e os scripts de apoio — com dados mockados e executável localmente. As fases foram iniciadas em sequência pelos prompts de continuação ("iniciar F2", "iniciar F3", "siga com a F4 e a F5", e assim por diante).
