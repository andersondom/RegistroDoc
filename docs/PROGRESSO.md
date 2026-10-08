# RegistroDoc — Progresso e continuidade

Data de referência: 2026-10-08.

## Confirmado
- Repositório GitHub: andersondom/RegistroDoc, branch principal main.
- README descreve MVP em validação final, .NET 10, Blazor, PostgreSQL 17, Docker Compose e xUnit.
- Código de src/RegistroDoc.Web/Components/Pages/Pesquisa.razor consultado em 2026-10-08.
- Homologação manual informada pelo usuário: 8/10 aprovados; ver HOMOLOGACAO.md.
- Esta documentação foi preparada para transferir o contexto ao Codex no VS Code.

## Não confirmado
- Correção das falhas 7 e 8: não implementada nesta transferência.
- Causa exata da falha de PDF: não diagnosticada.
- Resultado atual de build/testes: não executados nesta transferência.
- Disponibilidade e estado atual dos containers locais: desconhecidos.

## Próximas ações (prioridade)
- [ ] Abrir a solução no VS Code e verificar git status e branch atual.
- [ ] Ler AGENTS.md, REQUISITOS.md e HOMOLOGACAO.md.
- [ ] Criar branch de correção a partir da main atualizada.
- [ ] Investigar estado do formulário, query string, navegação e paginação em Pesquisa.razor.
- [ ] Investigar rota Web/API de PDF, autorização e resposta HTTP.
- [ ] Implementar correções pequenas e testes de regressão.
- [ ] Executar build, testes unitários e integração quando disponíveis.
- [ ] Subir ambiente fictício sem destruir volumes; repetir homologação.
- [ ] Registrar causas, arquivos, testes e evidências; solicitar aprovação para merge.

## Comandos não destrutivos úteis
- git status
- dotnet build RegistroDoc.slnx
- dotnet test RegistroDoc.slnx
- docker compose ps
- docker compose logs --tail 100 web api identityhub

## Registro de decisões
- Desenvolvimento principal migrado para Codex no VS Code.
- Contexto durável armazenado no repositório.
- Nenhum merge automático na main.
- Nenhum dado real até autorização e revisão de segurança.

Ao concluir cada etapa, atualizar esta seção com data, branch, commits e testes reais.
