# Guia de debug

> Sistema de Cotação de Projetos · versão 1.0.0 · 04/10/2026
> Para quem desenvolve: o que instalar, como configurar as chaves, como subir o ambiente e como depurar Backend e Frontend.

## 1. O que instalar

| Ferramenta | Versão | Para quê |
| --- | --- | --- |
| Git | 2.4x ou mais nova | Código. No Windows, o **Git for Windows** também traz o Git Bash e o OpenSSL usados pelos scripts |
| Docker Desktop (com WSL 2 no Windows) | Docker Engine 27 ou mais novo, com Compose v2 | Sobe MongoDB, Keycloak, Kong, Backend e Frontend |
| .NET SDK | 10.0 (testado com 10.0.401) | Build, testes e debug do Backend |
| Node.js com npm | 22 LTS ou mais novo (testado com 25.7) | Build, testes e debug do Frontend |
| OpenSSL | 3.x | Gerar a chave RSA do realm (já vem com o Git for Windows) |
| IDE | VS Code com **C# Dev Kit**, Visual Studio 2026 ou Rider | Pontos de parada no Backend |

Opcionais: **MongoDB Compass** (ver os dados), extensão **ESLint** e **Prettier** no VS Code.

No Windows, rode os scripts `./scripts/*.sh` no **Git Bash**. Confira as instalações:

```bash
git --version && docker --version && docker compose version && dotnet --version && node -v && openssl version
```

## 2. Configurar as chaves (`.env`)

O `.env` fica fora do Git e é lido pelo Docker Compose e pelos scripts.

```bash
cp .env.example .env
```

Preencha cada variável:

| Variável | O que colocar |
| --- | --- |
| `MONGO_ROOT_USER` / `MONGO_ROOT_PASSWORD` | Administrador do MongoDB. Usado só pelos scripts de init, backup e restauração |
| `MONGO_APP_DATABASE` | Nome do banco. Deixe `precificacao` |
| `MONGO_APP_USER` / `MONGO_APP_PASSWORD` | Usuário do Backend no banco, com acesso só ao banco da aplicação |
| `KEYCLOAK_ADMIN_USER` / `KEYCLOAK_ADMIN_PASSWORD` | Login do console do Keycloak (`http://localhost:8080`) |
| `KEYCLOAK_ADMIN_CLIENT_SECRET` | Segredo do cliente `precificacao-admin`, usado pelo Backend na gestão de usuários |
| `KEYCLOAK_TEST_USER_PASSWORD` | Senha dos usuários de teste `admin.teste`, `interno.teste` e `externo.teste` |
| `REALM_RSA_PRIVATE_KEY` | **Não preencha à mão.** Gerada pelo script abaixo |
| `ANTHROPIC_API_KEY` | Chave da Claude API ([console.anthropic.com](https://console.anthropic.com), em *API Keys*). Opcional para depurar o resto do sistema |

Para gerar senhas e segredos fortes:

```bash
openssl rand -base64 24
```

Gere a chave do realm. O script grava a chave privada no `.env` e a pública em `infra/kong/kong.yml`, que é versionado:

```bash
./scripts/gerar-chave-realm.sh
```

Cuidados:

- **Use senhas sem `$`, aspas ou espaços.** Elas vão para variáveis de ambiente e strings de conexão.
- **Sem `ANTHROPIC_API_KEY`,** catálogo, usuários e projetos funcionam, mas toda mensagem de cotação termina em `500 FALHA_PROCESSAMENTO`, com a mensagem fixa da RN12. O detalhe da falha fica no log do Backend.
- **O realm do Keycloak só é importado quando o contêiner é criado.** Se você mudar `KEYCLOAK_ADMIN_CLIENT_SECRET`, `KEYCLOAK_TEST_USER_PASSWORD` ou a chave do realm, recrie o contêiner com `docker compose up -d --force-recreate keycloak kong`. Usuários criados pela aplicação se perdem nessa recriação.
- **Os scripts de `infra/mongodb/init` só rodam com o volume vazio.** Trocar as senhas do MongoDB depois da primeira subida exige apagar o volume com `docker compose down -v`, o que apaga os dados. Antes, faça um backup.

## 3. Subir tudo no Docker

```bash
docker compose up -d --build
docker compose ps        # espere todos ficarem "healthy" (o Keycloak leva cerca de 1 minuto)
```

| Endereço | O que é |
| --- | --- |
| `http://localhost:3000` | Frontend |
| `http://localhost:8000` | API pelo Kong. O contrato está em `/openapi/v1.json` |
| `http://localhost:8080` | Keycloak. O console fica em `/admin`, com o `KEYCLOAK_ADMIN_USER` |

Backend e MongoDB não ficam expostos na máquina, só na rede interna do Docker.

Para entrar no sistema, use `admin.teste`, `interno.teste` ou `externo.teste`, com a senha `KEYCLOAK_TEST_USER_PASSWORD`.

Para ter dados de exemplo no catálogo:

```bash
./scripts/carregar-catalogo-referencia.sh
```

### Logs

```bash
docker compose logs -f backend          # JSON estruturado, com o traceId de cada erro
docker compose logs -f kong             # cada requisição e o status devolvido
docker compose logs -f keycloak         # login, tokens e importação do realm
```

Uma resposta de erro da API traz um `traceId`; procure-o no log do Backend para achar a exceção completa.

## 4. Depurar o Backend na IDE

O Backend roda na máquina, no depurador, e o resto continua no Docker. O arquivo `docker-compose.debug.yml` faz dois ajustes:

- publica o MongoDB em `127.0.0.1:27017`;
- faz o Kong encaminhar a API para `http://host.docker.internal:5080`, onde o Backend da IDE escuta.

### 4.1 Subir a infraestrutura em modo debug

```bash
docker compose stop backend frontend
docker compose -f docker-compose.yml -f docker-compose.debug.yml up -d --no-deps mongodb keycloak kong
```

### 4.2 Criar o `.env.debug`

Crie na raiz o arquivo `.env.debug`, que fica fora do Git (o padrão `.env.*` já está no `.gitignore`). Use os valores do seu `.env`:

```dotenv
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__MongoDB=mongodb://<MONGO_APP_USER>:<MONGO_APP_PASSWORD>@localhost:27017/precificacao?authSource=precificacao
Keycloak__AdminClientSecret=<KEYCLOAK_ADMIN_CLIENT_SECRET>
Anthropic__ApiKey=<ANTHROPIC_API_KEY>
```

O resto já vem do `appsettings.json` e do `appsettings.Development.json`: Keycloak em `http://localhost:8080/realms/precificacao`, HTTP permitido e log em texto simples.

### 4.3 Iniciar com depuração

**VS Code:** crie `.vscode/launch.json`. A pasta `.vscode` é local e não vai para o Git.

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Backend (debug)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-backend",
      "program": "${workspaceFolder}/backend/src/ProjectPricing.Api/bin/Debug/net10.0/ProjectPricing.Api.dll",
      "cwd": "${workspaceFolder}/backend/src/ProjectPricing.Api",
      "envFile": "${workspaceFolder}/.env.debug",
      "env": { "ASPNETCORE_URLS": "http://localhost:5080" }
    }
  ]
}
```

Crie também o `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build-backend",
      "command": "dotnet",
      "type": "process",
      "args": ["build", "${workspaceFolder}/backend/src/ProjectPricing.Api/ProjectPricing.Api.csproj"],
      "problemMatcher": "$msCompile"
    }
  ]
}
```

Depois, pressione **F5**.

**Visual Studio ou Rider:** abra `backend/ProjectPricing.sln` e use o perfil `http` do `launchSettings.json`, que já escuta em `http://localhost:5080`. Coloque as variáveis do `.env.debug` nas variáveis de ambiente do perfil, sem gravar o arquivo com segredos no Git.

**Sem IDE,** no Git Bash:

```bash
set -a; . ./.env.debug; set +a
dotnet run --project backend/src/ProjectPricing.Api --launch-profile http
```

### 4.4 Conferir

- `http://localhost:5080/health` responde `Healthy`.
- O Frontend em `http://localhost:3000` passa a falar com o Backend da IDE pelo Kong, e os pontos de parada são atingidos.

Bons pontos de parada:

| Onde | Para ver |
| --- | --- |
| `AgenteProjetos.ProcessarAsync` | Cada rodada da conversa: interpretação, RN09, cálculo e gravação |
| `ClassificadorTermos.Classificar` | A similaridade de cada termo com o catálogo (RN09) |
| `ServicoPrecificacao.CalcularItem` | A conversão e o arredondamento de cada item (RN05 e RN08) |
| `KeycloakAdminClient` | As chamadas à Keycloak Admin API (RF10) |
| `MapeadorDeErros.Mapear` | Que exceção virou qual `code` e status |

## 5. Depurar o Frontend

O Frontend precisa rodar na **porta 3000**, porque só ela está liberada no Keycloak (redirecionamento do login) e no CORS do Kong.

```bash
docker compose stop frontend         # obrigatório: veja o aviso abaixo
cd frontend
npm ci
npm run dev -- --port 3000 --strictPort
```

> **Atenção (Windows):** o Vite escuta em `::1:3000` e o contêiner em `0.0.0.0:3000`, os dois ao mesmo tempo, sem erro de porta ocupada. Com o contêiner ligado, o navegador pode abrir qualquer um deles. Pare sempre o contêiner `frontend` antes. Ao terminar, encerre o Vite com **Ctrl+C** no terminal dele.

O `public/env.js` já aponta para o Kong (`http://localhost:8000`) e para o Keycloak. As alterações no código aparecem na hora (*hot reload*).

Para depurar:

- **DevTools do navegador (F12):**
  - Em *Sources*, coloque pontos de parada nos arquivos `.tsx`.
  - Em *Network*, veja as chamadas à API. As cotações chegam em `text/event-stream`, com os eventos `delta`, `cotacao`, `erro` e `fim`.
- **VS Code:** crie uma configuração `"type": "chrome"` (ou `"msedge"`) com `"url": "http://localhost:3000"` e `"webRoot": "${workspaceFolder}/frontend"`.

Backend na IDE e Frontend no Vite podem rodar juntos: siga a seção 4 e depois esta.

## 6. Chamar a API direto

O Kong exige o token do Keycloak. A forma mais simples de obtê-lo é pelo próprio sistema:

1. Faça login em `http://localhost:3000`.
2. No DevTools, aba *Network*, abra uma chamada a `localhost:8000` e copie o cabeçalho `Authorization` (`Bearer eyJ...`).

```bash
TOKEN='eyJ...'
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:8000/api/v1/usuarios/me
```

O token vale poucos minutos; quando expirar, o Kong responde `401` e você copia outro.

No Git Bash:

- **Texto com acento no corpo:** grave o JSON num arquivo e envie com `--data-binary @arquivo.json`. Com `-d '...'`, o Git Bash pode estragar a codificação.
- **Caminhos em `docker run`:** use `MSYS_NO_PATHCONV=1` antes do comando.

## 7. Banco de dados

**Em modo debug** (seção 4), use o MongoDB Compass com:

```
mongodb://<MONGO_APP_USER>:<MONGO_APP_PASSWORD>@localhost:27017/precificacao?authSource=precificacao
```

**No modo normal,** o banco não fica exposto; use o shell de dentro do contêiner:

```bash
set -a; . ./.env; set +a
docker compose exec mongodb mongosh -u "$MONGO_APP_USER" -p "$MONGO_APP_PASSWORD" --authenticationDatabase precificacao precificacao
```

As coleções são `materiais`, `projetos` e `conversas`. Os usuários ficam no Keycloak.

## 8. Testes

```bash
dotnet test backend/ProjectPricing.sln                                      # todos os testes do Backend
dotnet test backend/tests/ProjectPricing.Aplicacao.Tests --filter "FullyQualifiedName~SuiteRegressao"   # só os 30 casos de referência
node scripts/regressao/calcular-esperados.mjs                               # confere os valores esperados da regressão
cd frontend && npm test                                                     # testes do Frontend
./scripts/verificar.sh                                                      # tudo, como antes de um PR
```

Os testes não precisam do Docker nem da chave da Claude API: o LLM é simulado.

## 9. Voltar ao modo normal

```bash
docker compose up -d --force-recreate mongodb kong   # desfaz os ajustes do modo debug
docker compose up -d                                 # religa backend e frontend
```

## 10. Problemas comuns

| Sintoma | Causa e solução |
| --- | --- |
| `docker compose up` falha com "rode ./scripts/gerar-chave-realm.sh" ou "defina … no .env" | Falta uma variável no `.env` (seção 2) |
| Keycloak demora ou o Backend fica esperando | O Keycloak leva cerca de 1 minuto para ficar `healthy`, e o Backend só sobe depois dele |
| `401 {"message":"Unauthorized"}` do Kong | Token ausente ou expirado, ou a chave do realm mudou sem recriar `keycloak` e `kong` |
| Login funciona, mas a API responde 401 com `code = NAO_AUTENTICADO` | O token não tem `aud=precificacao-api`. Recrie o contêiner do Keycloak para reimportar o realm |
| Toda cotação responde "Não foi possível processar essa mensagem…" | Falta `ANTHROPIC_API_KEY`, ou a chave é inválida. Veja o log do Backend |
| Gestão de usuários responde 500 | `KEYCLOAK_ADMIN_CLIENT_SECRET` diferente do que foi importado no realm. Recrie o Keycloak |
| `npm ci` falha com `EBUSY` | O OneDrive ou a IDE está segurando um arquivo em `node_modules`. Feche o que estiver usando a pasta e rode de novo |
| Porta 3000, 8000, 8080 ou 27017 ocupada | Outro programa usa a porta. No PowerShell: `Get-NetTCPConnection -LocalPort 3000 -State Listen` mostra o processo |
| Kong responde 502 em modo debug | O Backend da IDE não está rodando em `http://localhost:5080` |
| Mudanças no Frontend não aparecem em `localhost:3000` | O contêiner `frontend` ainda está ligado junto com o Vite (seção 5) |
