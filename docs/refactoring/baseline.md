# Baseline (antes da refatoração)

Registrado na branch `refactoring`, a partir do commit `73ccef1` da `main`.

| Métrica | Valor |
| --- | --- |
| Testes do backend (xUnit) | 375 aprovados, 0 falhas (Dominio 116, Aplicacao 92, Infraestrutura 51, Api 116) |
| Código de produção do backend | 4.446 linhas C# |
| Código de testes do backend | 4.179 linhas C# |
| Código do frontend (src) | 2.916 linhas TS/TSX |
| Maior arquivo | `AgenteProjetos.cs` (374 linhas) |
| `catch (Exception)` genéricos | 3 (`AgenteProjetos`, `CondutorConversa`, `MongoHealthCheck` filtrado) |
| Entidades com `TenantId` | 0 |
| Pontos com `Idempotency-Key` | 0 |
| Plugins de proteção no Kong | `jwt`, `cors` (sem rate limiting) |
| Limites de tamanho/uso do LLM | Só validação de descrição; sem cota por usuário |

Comando de verificação: `dotnet test backend/ProjectPricing.sln` (≈ 25 s).

## Regra do jogo

Os testes existentes são o *golden master*: só podem mudar quando a mudança for de
assinatura imposta pela refatoração (ex.: construtor do repositório) e nunca em asserções
de comportamento.
