# RegistroDoc — Progresso e continuidade

Data de referência: 2026-10-08.

## Confirmado
- Repositório GitHub: andersondom/RegistroDoc, branch principal main.
- README descreve MVP em validação final, .NET 10, Blazor, PostgreSQL 17, Docker Compose e xUnit.
- Código de src/RegistroDoc.Web/Components/Pages/Pesquisa.razor consultado em 2026-10-08.
- Homologação manual informada pelo usuário: 8/10 aprovados; ver HOMOLOGACAO.md.
- Esta documentação foi preparada para transferir o contexto ao Codex no VS Code.

## Marco de desenvolvimento — 2026-10-08
Branch: `fix/pesquisa-paginacao-pdf`, criada a partir de `docs/handoff-codex-20261008` (árvore inicialmente limpa). Alterações e validação registradas nesta branch, sem merge ou publicação. Consulte `git log -1` para o identificador do commit local.

### Causas comprovadas no código e na configuração
- Pesquisa: links incluíam Termo/AnoReferencia, mas o componente não recebia query string. O submit mudava apenas `_pagina`, mantendo a rota anterior. O formulário foi substituído por GET para `/pesquisa`, com parâmetros recebidos da URL. Paginação conserva filtros; novas pesquisas reiniciam na página 1; campos vazios deixam de incluir filtros nos links e chamadas da API.
- PDF: `PdfFileInspector` calcula caminhos relativos ao InputPath de cada indexador. No Compose, os InputPath incluem ano ou benchmark/ano, enquanto Documents:RootPath da API aponta para a raiz comum. A API procurava o arquivo na pasta errada. A existência da rota Web e das exigências de autorização foi confirmada; não havia rota faltante.
- Menu: faltava importar o namespace de AuthorizeView, gerando três avisos de compilação e tratando os elementos como markup. Import corrigido.

### Correções implementadas
- Pesquisa usa renderização no servidor por requisição e formulário GET; evita dependência de HttpContext do circuito para obter token e mantém URL coerente com resultados.
- `DocumentoFileResolver` na Application aceita caminho relativo normal e os caminhos legados de ano/benchmark do Compose. Verifica SHA-256 antes de servir o arquivo e rejeita caminhos fora da raiz. Não altera registros existentes. Compatibilidade limitada às pastas documentadas; outras configurações precisam de caminho relativo à raiz comum.
- Proxy Web de PDF continua protegido por RequireAuthorization; API continua com Authorize. Resposta PDF recebe `Cache-Control: no-store, private` e mantém abertura inline em nova aba e suporte a ranges.
- Mensagem de falha da pesquisa não expõe detalhes internos da exceção.
- Testes de regressão cobrem filtros, URLs, troca e limpeza de pesquisa, localização de PDFs nas três pastas, hash divergente, escape da raiz e token da requisição/ausência após logout. Testes de componente exercitam seu ciclo de parâmetros; não substituem navegação real no navegador.

### Verificação executada
- `dotnet build RegistroDoc.slnx --no-restore`: execução final aprovada, zero avisos e zero erros (.NET SDK 10.0.303).
- `dotnet test tests/RegistroDoc.UnitTests/RegistroDoc.UnitTests.csproj --no-restore`: execução final fora da sandbox, 6 aprovados, 0 falhas. Inclui o teste placeholder preexistente.
- `dotnet test RegistroDoc.slnx --no-restore`: tentativa inicial bloqueou carregamento dos unitários pela política de Controle de Aplicativo; integração teve 2 aprovados e 3 falhas por ausência de `ConnectionStrings__RegistroDoc`. Não foi configurada conexão com banco persistido, pois os testes de pesquisa inserem/excluem registros e isso requer autorização explícita. A suíte completa não está aprovada.
- `docker compose ps`: executado com permissão; postgres saudável e Web/API/IdentityHub/indexadores ativos. Containers não foram reconstruídos e continuam com a versão anterior.
- `git diff --check`: sem erros de whitespace.

### Pendências e limites
- Conferir visualmente no navegador os cliques da paginação/pesquisa, abertura do PDF em nova aba e voltar/avançar. O roteiro HTTP passou; não declarar homologação manual 10/10 nesta etapa. Sessão expirada permanece como verificação adicional.
- Integração já executada e aprovada no banco isolado; continuar usando `compose.validation.yaml`. Não executar o serviço de testes do Compose principal automaticamente contra o banco persistido atual.
- Unificar futuramente a raiz de metadados dos indexadores com Documents:RootPath, preservando os registros legados. A compatibilidade de leitura resolve a configuração atual sem migração.
- A conferência de hash lê o PDF antes de servir: avaliar custo com PDFs maiores na etapa de carga. Proteção contra links simbólicos e acervo real permanece parte da revisão de segurança de produção.
- Revisar o diff e obter aprovação explícita antes de merge na main.

## Marco de validação local — 2026-10-08

- Atualizados somente Web/API com `docker compose up -d --build --no-deps web api`; Web reconstruído novamente após ajustes da página inicial e RedirectToLogin. Banco, volumes e indexadores existentes preservados.
- A validação encontrou Home.razor ainda com o template Hello world. Corrigido para encaminhar à pesquisa (ou login se anônimo). RedirectToLogin passa a navegar em OnInitialized, compatível com renderização estática no servidor.
- Adicionado `tests/validar_homologacao_http.py`, reproduzível com Python padrão e credenciais do ambiente ou `.env` local; não imprime segredos. Fluxo autenticado e URLs de PDF antes/depois de logout aprovados. Detalhes e limites em HOMOLOGACAO.md.
- Adicionado `compose.validation.yaml`: projeto Docker separado, banco temporário em tmpfs, sem publicação de portas ou volumes do banco atual. A API prepara schema e serventia fictícia; JWT signing key gerada na execução, sem chave versionada. Runner copia apenas src/tests/solução para sua área de trabalho, preservando a árvore local.
- `docker compose -f compose.validation.yaml up --abort-on-container-exit --exit-code-from tests`: suíte completa aprovada, 6 unitários e 5 de integração, zero falhas. Os containers de validação foram parados pelo próprio Compose após o término; o ambiente principal permanece ativo. Resultado substitui a limitação da tentativa inicial registrada acima.
- `dotnet build RegistroDoc.slnx --no-restore`: execução após ajustes finais aprovada, zero avisos e zero erros.
- `python -u tests/validar_homologacao_http.py`: aprovado; cobre também redirecionamento inicial autenticado e GET com campos vazios. Validação por HTTP, sem navegador automatizado.
- Alterações preparadas para revisão na branch de correção; sem merge na main.

## Próximas ações (prioridade)
- [x] Verificar git status e branch atual.
- [x] Ler AGENTS.md e todos os documentos de docs, README, compose e solução.
- [x] Criar branch de correção preservando a branch de handoff; main não alterada.
- [x] Investigar estado do formulário, query string, navegação e paginação em Pesquisa.razor.
- [x] Investigar rota Web/API de PDF, autorização e configuração de arquivos.
- [x] Implementar correções pequenas e testes de regressão.
- [x] Executar build, testes unitários e integração em banco isolado.
- [x] Atualizar Web/API sem destruir volumes; repetir roteiro por HTTP com massa fictícia.
- [ ] Concluir conferência visual no navegador e verificação adicional de sessão expirada.
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
