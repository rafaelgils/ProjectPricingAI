# Guia de uso

> Sistema de Cotação de Projetos · versão 1.0.0 · 04/10/2026
> Para quem usa o sistema no dia a dia: clientes (internos e externos) e administradores.

## 1. Acesso

1. Abra `http://localhost:3000`. Você será levado à tela de login.
2. Entre com o usuário e a senha recebidos do administrador. Não há autocadastro.
3. No primeiro acesso, o sistema pede que você troque a senha temporária.

O menu mostra só o que o seu papel pode usar:

| Papel | Projetos | Catálogo | Usuários |
| --- | --- | --- | --- |
| Cliente externo | Os próprios | — | — |
| Cliente interno | Os próprios | Consulta dos materiais ativos | — |
| Admin | Todos | Cadastro, alteração, inativação e reativação | Cadastro, alteração e desativação |

Para sair, use **Sair** no canto superior direito.

## 2. Pedir uma cotação

1. Em **Projetos**, clique em **Nova cotação**.
2. Descreva o projeto em texto livre, com as medidas de cada item. Exemplos:
   - "Placa de trânsito 60x60 cm com película refletiva"
   - "Duas placas de 50x70 cm com película refletiva, um poste de 3 m e 2 abraçadeiras"
   - "2 m² de banner e 4 horas de instalação"
3. Clique em **Cotar**. A resposta aparece aos poucos na conversa e termina com o valor estimado, por exemplo: "Para esse projeto o valor estimado é R$ 52,58."
4. A tabela ao lado mostra cada item com a quantidade, o preço unitário e o subtotal, todos com 2 casas decimais. Dá para refazer a conta de cada linha com o que está na tela.

**Medidas aceitas.** O sistema converte para a unidade do material:

| Material vendido em | Você pode informar |
| --- | --- |
| metro (m) | mm, cm ou m |
| metro quadrado (m²) | mm², cm², m² ou largura × altura (ex.: 60 x 60 cm) |
| litro (L) | ml ou L |
| hora (h) | minutos ou horas |
| unidade (un) | quantidade |

Com largura × altura, a quantidade é o número de peças: "2 placas de 60 x 60 cm" são 0,72 m².

## 3. Quando o sistema pergunta antes de calcular

| Situação | O que aparece | O que fazer |
| --- | --- | --- |
| Falta uma medida ou quantidade | Uma pergunta, como "Quais são a largura e a altura da placa?" | Responda na conversa, por exemplo "60 x 60 cm" |
| O termo é parecido com um material, mas não igual | "Antes de calcular, confirme: você quis dizer "Película refletiva grau técnico" para "película reflexiva"?" | Confirme ("sim") ou diga o material certo |
| O assistente sugere um material pelo sentido | "… pode ser "Tubo de aço galvanizado 2 polegadas" (sugerido pelo assistente)?" | Confirme ou recuse. Uma sugestão nunca entra no valor sem a sua confirmação |
| Um item não existe no catálogo | "Não encontrei no catálogo: …" | Ajuste a descrição ou peça ao administrador que cadastre o item |
| A medida não serve para o material | Por exemplo, tinta (vendida em litros) pedida em m² | Informe a medida na unidade certa |

Enquanto houver pergunta pendente, a cotação anterior do projeto continua valendo.

## 4. Refinar uma cotação

Abra o projeto na lista e continue a conversa (botão **Enviar**) para:

- mudar quantidades ou medidas ("troque para 6 m de poste");
- incluir itens ("inclua 2 abraçadeiras");
- remover itens ("retire a instalação");
- trocar um material por outro.

Os itens que já estavam na cotação mantêm o preço da primeira cotação, mesmo que o catálogo mude depois. Itens novos entram com o preço atual do catálogo.

## 5. Excluir (arquivar) um projeto

Na lista de projetos, use **Excluir** e confirme. Nada é apagado: o projeto fica arquivado, continua visível, mas fica somente leitura: qualquer nova mensagem recebe "Este projeto está inativo."

## 6. Catálogo (Admin)

- **Novo material:** nome, tipo (material ou serviço), categoria, unidade, preço unitário (2 casas) e fornecedor são obrigatórios. Os sinônimos são opcionais, separados por vírgula, e ajudam o sistema a reconhecer a descrição do cliente (ex.: "placa" para "Chapa de aço galvanizado nº 18").
- O nome e os sinônimos não podem repetir o nome nem o sinônimo de outro material, sem diferenciar maiúsculas e acentos.
- **Inativar** tira o material do catálogo sem apagar o histórico; as cotações já feitas não mudam. Para reativar, use **Alterar** e mude o status para ativo.

## 7. Usuários (Admin)

- **Novo usuário:** nome de usuário (sem espaços), nome, sobrenome, e-mail, papel e uma senha temporária de no mínimo 8 caracteres. O usuário troca essa senha no primeiro acesso.
- **Alterar:** muda nome, sobrenome, e-mail, papel e a situação (ativo ou inativo).
- **Desativar:** o usuário deixa de entrar no sistema; nada é apagado.
- Você não pode desativar o seu próprio usuário nem tirar de si o papel de admin, para o sistema não ficar sem administrador.

## 8. Mensagens de erro

| Mensagem | Significado |
| --- | --- |
| "Não foi possível processar essa mensagem no momento, favor contate o administrador" | Falha no assistente. O projeto fica como estava; tente de novo mais tarde ou avise o administrador |
| "O projeto foi alterado por outra mensagem. Recarregue o projeto e tente de novo." | Duas mensagens ao mesmo tempo no mesmo projeto |
| "Este projeto está inativo." | O projeto está arquivado |
| "Acesso negado." | O seu papel não permite essa ação |

## 9. Operação (administrador técnico)

- **Subir o ambiente:** `docker compose up -d` na raiz do repositório, com o `.env` preenchido a partir do `.env.example`.
- **Catálogo de referência:** `./scripts/carregar-catalogo-referencia.sh` carrega os materiais de exemplo usados na suíte de regressão (preços pesquisados em tabelas públicas como SINAPI e SICRO, em 04/10/2026). Só insere o que ainda não existe; materiais com nome ou sinônimo já usados são pulados e listados.
- **Backup e restauração:** `./scripts/backup-mongodb.sh` e `./scripts/restaurar-mongodb.sh <arquivo> --confirmar`.
- **Verificação completa antes de publicar:** `./scripts/verificar.sh`.
- **Contrato da API:** `http://localhost:8000/openapi/v1.json`, pelo Kong.
