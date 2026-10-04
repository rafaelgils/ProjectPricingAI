# Stack tecnológica

> Sistema de Cotação de Projetos · versão 0.8 · 04/10/2026
> Versões e bibliotecas permitidas. Incluir uma biblioteca fora desta lista exige aprovação do Tech Lead e atualização deste arquivo no mesmo PR.

**Regra de versões:** usar sempre a última versão de *patch* da linha indicada. Mudança de versão *major* exige ADR em `architecture.md`. Todas as versões abaixo foram confirmadas em 04/10/2026.

## 1. Plataforma

| Componente | Tecnologia | Versão | Observação |
| --- | --- | --- | --- |
| Runtime do Backend | .NET (ASP.NET Core) | **10 (LTS)**, última versão de patch | Lançado em 11/11/2025, suporte até 14/11/2028 ([fonte](https://endoflife.ai/dotnet/10)) |
| Identidade | Keycloak | **26.x** (estável atual: 26.8.0, de 01/10/2026) | ([fonte](https://versionlog.com/keycloak/)) |
| Banco de dados | MongoDB | **8.0** | Imagem `mongo:8.0` |
| Driver do banco | MongoDB.Driver (C#) | **3.x** | Linha 3 serializa `decimal` como `Decimal128` por padrão ([fonte](https://www.mongodb.com/docs/drivers/csharp/current/reference/upgrade/v3/)) |
| Frontend | React | **19.x** (estável atual: 19.3.0, de 09/09/2026) | ([fonte](https://versionlog.com/react/)) |
| Linguagem do Frontend | TypeScript | **5.x** | Modo `strict` |
| Build do Frontend | Vite | Versão estável atual | — |
| Node.js (build e ferramentas) | Node.js | Linha LTS ativa | Apenas para build e testes; não roda em produção |
| API Gateway | **Kong Gateway OSS** (edição open source) | **3.9.x** (última: 3.9.3) | A 3.9 é a última linha com imagens OSS publicadas; a Kong não publicou imagens OSS da 3.10 em diante ([versões](https://www.versio.io/en/product-release-end-of-life-eol-kong-inc-kong-gateway.html), [discussão](https://github.com/Kong/kong/discussions/14405)). Valida o JWT com a chave pública fixa do realm (ADR-010) |
| Protocolo das tools | Model Context Protocol, SDK oficial C# | Versão estável atual no NuGet | Pacotes `ModelContextProtocol` e `ModelContextProtocol.AspNetCore` ([documentação](https://csharp.sdk.modelcontextprotocol.io/v2/)) |
| Contêineres | Docker Engine + Docker Compose (v2, comando `docker compose`) | Versão estável atual | Padrões de uso em `standards.md` |
| Registro de imagens | Nenhum por enquanto | — | Imagens próprias (`frontend`, `backend`) com tag semver, mantidas no Docker instalado na máquina |
| Cofre de segredos | AWS Secrets Manager | — | Fora do ambiente local: guarda a chave da Claude API, as senhas do MongoDB e do Keycloak e a chave privada do realm. No ambiente local, `.env` fora do Git |
| CI | — | — | Não há pipeline de CI no MVP |
| LLM | **Anthropic Claude Opus 5.5** | ID do modelo: `claude-opus-5-5` | Via Claude API; janela de contexto de 1M tokens ([fonte](https://platform.claude.com/docs/en/models/overview)). Decisão no ADR-008 |

## 2. Imagens de contêiner

Um contêiner por camada (frontend, backend, banco), mais as imagens oficiais de Keycloak e Kong. Tags sempre fixas na linha indicada, nunca `latest`.

| Contêiner | Imagem base | Uso |
| --- | --- | --- |
| `backend` (build) | `mcr.microsoft.com/dotnet/sdk:10.0` | Etapa de compilação e testes |
| `backend` (execução) | `mcr.microsoft.com/dotnet/aspnet:10.0` | Executa a API .NET |
| `frontend` (build) | `node` na linha LTS ativa | Gera o build estático do React |
| `frontend` (execução) | `nginx` na versão estável | Serve os arquivos estáticos |
| `mongodb` | `mongo:8.0` | Banco de dados, com volume persistente |
| `keycloak` | `quay.io/keycloak/keycloak:26.x` | Identidade, imagem oficial sem customização |
| `kong` | `kong:3.9` | API Gateway OSS, imagem oficial sem customização |

## 3. Bibliotecas permitidas — Backend

| Finalidade | Biblioteca |
| --- | --- |
| Autenticação JWT | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Acesso a dados | `MongoDB.Driver` |
| Tools do agente | `ModelContextProtocol`, `ModelContextProtocol.AspNetCore` |
| Cliente do LLM | `Anthropic` (SDK oficial C# da Anthropic) ([documentação](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/csharp)) |
| Abstração de LLM | `Microsoft.Extensions.AI` (`IChatClient`, implementado pelo SDK `Anthropic`) |
| Validação de entrada | `FluentValidation` |
| Documentação da API | OpenAPI nativo do ASP.NET Core (`Microsoft.AspNetCore.OpenApi`), sem Swagger UI |
| Logs | `Microsoft.Extensions.Logging` |
| Resiliência de chamadas externas | `Microsoft.Extensions.Http.Resilience` |

## 4. Bibliotecas permitidas — Frontend

| Finalidade | Biblioteca |
| --- | --- |
| Login OIDC + PKCE | `oidc-client-ts` + `react-oidc-context` |
| Rotas | `react-router` |
| Estado de servidor e cache | `@tanstack/react-query` |
| Formulários e validação | `react-hook-form` + `zod` |
| Streaming (SSE) | `EventSource` nativo do navegador ou `fetch` com leitura de stream |
| Build com Vite | `@vitejs/plugin-react` |
| Qualidade de código | `eslint`, `typescript-eslint`, `prettier` |

## 5. Testes e qualidade

| Escopo | Ferramenta |
| --- | --- |
| Unitário (.NET) | `xUnit` |
| Mocks (.NET) | `Moq` (4.20.2 ou superior; a 4.20.0 incluía o componente SponsorLink, retirado depois) |
| Cobertura (.NET) | `coverlet.collector` |
| Componentes (React) | `Vitest` + `@testing-library/react`, com `@testing-library/jest-dom` (asserções) e `@testing-library/user-event` (interação) |
| Ambiente de DOM nos testes (React) | `jsdom` |
| Cobertura (React) | `@vitest/coverage-v8` |
| Análise de dependências | `dotnet list package --vulnerable` e `npm audit`, executados localmente |

## 6. Restrições

- Proibidas bibliotecas com licença comercial ou restritiva sem aprovação formal. Atenção a pacotes que mudaram de licença recentemente; conferir a licença da versão antes de adicionar.
- Proibido `double`/`float` para valores monetários (ver `standards.md`).
- Proibido acessar o MongoDB a partir das tools MCP sem passar pelos serviços de domínio.
- Proibido que testes acessem banco real ou subam contêineres (ver `standards.md`).
- Proibido usar imagens base não oficiais ou com tag `latest`.
- Nenhuma dependência de serviço externo de busca na internet (ADR-002).

## Fontes

- [.NET 10 — ciclo de vida](https://endoflife.ai/dotnet/10)
- [Keycloak — versões](https://versionlog.com/keycloak/)
- [React — versões](https://versionlog.com/react/)
- [MongoDB C# Driver v3 — guia de atualização](https://www.mongodb.com/docs/drivers/csharp/current/reference/upgrade/v3/)
- [MCP C# SDK — documentação](https://csharp.sdk.modelcontextprotocol.io/v2/)
- [Kong Gateway — versões](https://www.versio.io/en/product-release-end-of-life-eol-kong-inc-kong-gateway.html)
- [Kong OSS — discussão sobre imagens 3.10+](https://github.com/Kong/kong/discussions/14405)
- [Claude — visão geral dos modelos](https://platform.claude.com/docs/en/models/overview)
- [Claude — SDK C#](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/csharp)
