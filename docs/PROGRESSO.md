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

## Ajustes de usabilidade — 2026-10-08

- Solicitação do usuário: evitar rolagem até o fim para paginar e remover a navegação Início redundante.
- Pesquisa.razor exibe os mesmos controles Anterior/Próxima acima e abaixo dos resultados. Um RenderFragment compartilhado mantém URLs, filtros e estados desabilitados iguais nas duas posições. Sem paginação em resultados vazios ou de página única.
- NavMenu.razor remove o item Início. A marca RegistroDoc permanece como acesso à raiz; os itens autenticados são Pesquisa/Sair e o item anônimo é Entrar.
- Web atualizado com `docker compose up -d --build --no-deps web`, preservando banco e volumes.
- `dotnet build RegistroDoc.slnx --no-restore`: aprovado, zero avisos/erros.
- `dotnet test tests/RegistroDoc.UnitTests/RegistroDoc.UnitTests.csproj --no-build --no-restore`: 6 aprovados, zero falhas.
- `python -u tests/validar_homologacao_http.py`: aprovado; roteiro ampliado confere duas paginações idênticas antes/depois dos cards de resultado e ausência do item Início, além do fluxo de pesquisa/PDF/logout existente. Script usa loopback IPv4 para evitar demora de resolução de localhost neste ambiente.
- `git diff --check`: sem erros. Ajustes locais na branch `fix/pesquisa-paginacao-pdf`, posteriores ao commit `154f486`; nenhum merge.
- Conferência visual pelo usuário permanece pendente; integração em banco isolado aprovada no marco anterior, não repetida para esta alteração de apresentação.

## Limpeza do layout — 2026-10-08

- Removido o link About do MainLayout: era um resíduo do template que apontava à documentação do ASP.NET, sem função no RegistroDoc.
- Removida também a faixa superior vazia e os estilos correspondentes. Conteúdo mantém espaçamento superior; alteração vale para login e demais páginas do layout compartilhado.
- `dotnet build RegistroDoc.slnx --no-restore`: aprovado, zero avisos/erros.
- `dotnet test tests/RegistroDoc.UnitTests/RegistroDoc.UnitTests.csproj --no-build --no-restore`: 6 aprovados, zero falhas.
- Atualizado somente o Web com `docker compose up -d --build --no-deps web`, preservando banco e volumes. GET `/login` aprovado: HTTP 200, formulário de login presente e link About ausente.
- `python -u tests/validar_homologacao_http.py`: fluxo de login, pesquisa, paginação no topo/final, PDF e logout aprovado. `git diff --check`: sem erros. Alterações locais na branch de correção, sem merge. Conferência visual permanece a cargo da homologação.

## Identidade visual — 2026-10-08

- Solicitação do usuário: visual com mais cores e efeitos. Aplicada paleta azul profundo/violeta/ciano, fundos em gradiente, sombras, cartões arredondados, destaque de resultado ao passar o mouse e botões com gradiente.
- Login redesenhado com apresentação do acervo, ilustração decorativa criada em CSS e painel de autenticação. Campos, nomes, endpoint e comportamento de login preservados.
- Marca RD e navegação com destaque ativo; pesquisa com cabeçalho e formulário destacados. Paginação no topo/final e fluxos de PDF/logout preservados.
- Layout responsivo, fontes locais, foco visível e estilos de movimento reduzido. HTML identificado como pt-BR. Nenhuma imagem, fonte ou biblioteca externa adicionada.
- `dotnet build RegistroDoc.slnx --no-restore`: aprovado, zero avisos/erros. `dotnet test tests/RegistroDoc.UnitTests/RegistroDoc.UnitTests.csproj --no-build --no-restore`: 6 aprovados, zero falhas.
- `docker compose up -d --build --no-deps web`: Web atualizado; banco e volumes preservados.
- `python -u tests/validar_homologacao_http.py`: aprovado, incluindo login, filtros, paginação, PDFs corretos e bloqueio após logout. Integração de banco não repetida para mudanças visuais.
- Login conferido visualmente no Edge headless em desktop (1440 px) e viewport móvel real de 390 px via protocolo de depuração. Medidas no móvel: viewport/documento 390 px, painel 342 px; sem excesso horizontal. Captura nativa inicial recortava a largura mínima da janela e foi substituída pela captura do viewport emulado.
- Capturas e perfil de conferência somente em `artifacts/visual` (ignorado pelo Git). Conferência visual das interações da pesquisa e aprovação estética pelo usuário permanecem pendentes; capturas de login não substituem homologação completa.
- `git diff --check`: sem erros. Alterações locais na branch de correção, sem merge na main.

## Próximas ações (prioridade)
- [x] Verificar git status e branch atual.
- [x] Ler AGENTS.md e todos os documentos de docs, README, compose e solução.
- [x] Criar branch de correção preservando a branch de handoff; main não alterada.
- [x] Investigar estado do formulário, query string, navegação e paginação em Pesquisa.razor.
- [x] Investigar rota Web/API de PDF, autorização e configuração de arquivos.
- [x] Implementar correções pequenas e testes de regressão.
- [x] Executar build, testes unitários e integração em banco isolado.
- [x] Atualizar Web/API sem destruir volumes; repetir roteiro por HTTP com massa fictícia.
- [x] Concluir conferência visual no navegador e verificação adicional de sessão expirada.
- [x] Registrar causas, arquivos, testes e evidências; consolidar commits locais.
- [x] Obter autorização explícita e realizar merge na main.

## Marco de continuidade — segurança e navegação — 2026-10-08

- Mantida a branch `fix/pesquisa-paginacao-pdf` e preservadas as alterações visuais locais preexistentes; nenhum merge na main.
- Corrigida pendência do README: login/logout desativavam antiforgery. Endpoints agora validam o token antes de autenticar/encerrar sessão, rejeitando token ausente/inválido com HTTP 400. Formulários incluem token; logout exige autenticação.
- Ampliado `tests/validar_homologacao_http.py` e criado `tests/validar_homologacao_navegador.py`, usando Python padrão e Edge com perfil próprio. Perfis/capturas somente em `artifacts/visual`, ignorado pelo Git; nenhum segredo impresso.
- `dotnet build RegistroDoc.slnx --no-restore`: aprovado, zero avisos/erros.
- `dotnet test tests/RegistroDoc.UnitTests/RegistroDoc.UnitTests.csproj --no-build --no-restore`: 4 aprovados e 2 bloqueados pelo Controle de Aplicativo do Windows; repetição fora da sandbox encontrou o mesmo bloqueio.
- `docker compose -f compose.validation.yaml up --abort-on-container-exit --exit-code-from tests`: aprovado, 6 unitários e 5 de integração, zero falhas; banco temporário separado do ambiente principal.
- `docker compose up -d --build --no-deps web`: aprovado; somente Web atualizado, banco/volumes preservados.
- `python -u tests/validar_homologacao_http.py`: aprovado; cobre tokens ausentes/inválidos, sessão preservada após logout rejeitado e fluxo existente de pesquisa/PDF/logout.
- `python -u tests/validar_homologacao_navegador.py`: execução final aprovada; cliques de paginação, histórico, troca de pesquisa na página 2, abertura de PDF em outra aba e bloqueio após logout. Primeiras execuções expuseram dois problemas do roteiro, corrigidos: seletor abrangia o article do layout e o clique precisava simular gesto de usuário para permitir nova aba.
- Pesquisa em desktop 1440 px/celular 390 px sem excesso horizontal; captura móvel inspecionada. Abertura da aba aprovada; renderização do PDF no visualizador ainda não homologada.
- Evidências atualizadas em HOMOLOGACAO.md e comandos no README. Aprovação visual humana, sessão expirada, revisão de produção (HTTPS, arquivos, logs, backup/restauração e carga) e aprovação explícita para merge continuam pendentes. MVP validado tecnicamente nesta etapa; não declarar pronto para acervo real.

## Marco de expiração e recuperação do ambiente — 2026-10-09

- Continuidade autorizada pelo usuário; branch `fix/pesquisa-paginacao-pdf` preservada, sem merge na main. Alterações anteriores mantidas.
- Extraída `SessionCookieConfiguration`, utilizada pelo Web e pelo teste. `SlidingExpiration=false` impede que a sessão Web ultrapasse a expiração original do token do IdentityHub.
- Adicionado `SessionExpirationTests`, usando handler real de cookies e relógio controlado: sessão válida antes do vencimento, sem renovação após metade do prazo, rejeição e redirecionamento ao login após expiração.
- Build final `dotnet build RegistroDoc.slnx --no-restore`: zero avisos e erros.
- Suíte final `docker compose -f compose.validation.yaml up --abort-on-container-exit --exit-code-from tests`: 7 unitários e 5 de integração aprovados. Primeira execução encontrou erro na asserção do teste, corrigido; resultado final não tem falhas.
- Imagem Web reconstruída; início na porta 8080 bloqueado por reserva de portas do Windows (faixa 8069–8168), comprovada por `netsh interface ipv4 show excludedportrange protocol=tcp`.
- Compose permite `WEB_PORT` com padrão 8080, mantendo loopback. `.env` local recebeu somente configuração `WEB_PORT=18080`; nenhum segredo versionado. `docker compose up -d --no-deps web`: Web iniciado em http://localhost:18080. Banco e volumes preservados; sem alterações nas reservas do Windows.
- Roteiros HTTP e Edge usam a porta configurada; ambos reexecutados e aprovados. README e HOMOLOGACAO atualizados.
- Limites: expiração testada no handler com relógio controlado; expiração em navegador/IdentityHub real ainda não executada. Aprovação humana do visual, renderização do PDF e requisitos de produção continuam pendentes. Merge exige aprovação explícita.

## Fechamento do MVP e pacote de operação — 2026-10-09

- Usuário aprovou visual e leitura do PDF e autorizou concluir as pendências. Destino pretendido: servidor local do cartório; sistema operacional/endereço/certificado ainda não fornecidos. Pacote de implantação preparado sem instalação no servidor.
- Revisadas as alterações locais de interface, paginação no topo/final, antiforgery, expiração e porta configurável, preservando o trabalho anterior na branch `fix/pesquisa-paginacao-pdf`. Sem merge ou envio ao GitHub.
- `.dockerignore` passou a excluir `artifacts` (perfis/capturas do navegador), diretórios locais de credenciais e arquivos de chave; imagens Web/API reconstruídas sem esses artefatos. Imagens antigas locais não foram publicadas nem removidas nesta etapa.
- `DocumentoFileResolver` rejeita links simbólicos de arquivo/pastas na raiz. Dois testes executados no Docker Linux; proteção depende também das permissões de escrita do acervo para evitar trocas concorrentes de arquivos.
- Web suporta caminho persistente de chaves de Data Protection e identifica a aplicação de forma estável.
- `compose.production.yaml`, `deploy/production.env.example` e OPERACAO.md preparados: HTTPS direto no Kestrel, host permitido, serviços internos sem publicação de portas, montagem somente leitura de acervo/certificado, chaves persistentes e limite de logs.
- `dotnet build RegistroDoc.slnx --no-restore`: final aprovado, zero avisos/erros. `docker compose -f compose.validation.yaml up --abort-on-container-exit --exit-code-from tests`: final aprovado, 9 unitários e 5 de integração.
- HTTP e Edge padrão reexecutados e aprovados na versão final. `validar_sessao_expirada.py`: expiração real no Edge/IdentityHub e `pg_dump`/`pg_restore` em banco temporário separado aprovados; ambiente separado parado ao terminar. Rota PDF usa ID fictício inexistente, conferindo a exigência de autorização antes/depois do vencimento.
- `validar_carga_http.py`: 40 consultas, concorrência 4, zero falhas, 3,734 s; p50 232,61 ms/p95 1.585,64 ms/máximo 1.674,99 ms durante outras verificações. Resultado local, sem garantia de escala de produção. JSON somente em artifacts/validation, ignorado pelo Git e Docker.
- `validar_configuracao_producao.py`: aprovado, verifica composição sem criar serviços nem usar segredos reais. HTTPS com certificado e hardware reais ainda depende da instalação.
- Última execução de `validar_sessao_expirada.py` também aprovou persistência da sessão após reiniciar o Web, com chaves em diretório próprio de artifacts/validation. Expiração real e restauração fictícia repetidas e aprovadas após esse reinício; nenhum segredo versionado.
- Dados e volumes do ambiente principal preservados; Web disponível em http://localhost:18080. Não houve migração destrutiva nem uso de acervo real.
- Critérios funcionais do MVP concluídos. Requisitos restantes de servidor, certificado, auditoria dos acessos (serviço existente não chamado pelos endpoints), política de backup/logs, permissões e carga representativa documentados em OPERACAO.md; não declarar instalação de produção concluída. Merge na main exige autorização explícita.
- Commit local das correções: `986ba1f` — interface, antiforgery, expiração, persistência de chaves, proteção de PDF e roteiros de regressão. A consolidação do pacote de operação/documentação foi interrompida e retomada no marco de 2026-10-13. Nenhum envio ao GitHub; main preservada.

## Consolidação da entrega — 2026-10-13

- Retomada solicitada para concluir o MVP antes dos próximos updates. Confirmados branch `fix/pesquisa-paginacao-pdf`, commit funcional `986ba1f` e pacote de operação/documentação ainda sem commit; sem novas alterações no código funcional.
- Evidências de 2026-10-09 preservadas: build sem avisos/erros, 9 unitários e 5 de integração aprovados, HTTP/Edge, expiração real, sessão preservada após reinício, restauração fictícia e carga moderada. Esses testes não foram declarados reexecutados em 2026-10-13.
- Em 2026-10-13, `python -u tests/validar_configuracao_producao.py` reexecutado e aprovado; `git diff --check` sem erros. A verificação do Compose não inicia containers nem instala o sistema no servidor.
- Consolidar em commit local separado o Compose de produção/validação de sessão, configuração de porta, roteiros operacionais e documentação. Consultar `git log -2 --oneline` para os identificadores finais. Segredos, perfis, chaves, PDFs e dumps permanecem fora do versionamento.
- MVP funcional entregue para uso com dados fictícios; próximos updates devem partir desta entrega e ter escopo próprio. Instalação em servidor/acervo real depende dos requisitos explícitos em OPERACAO.md, incluindo política de auditoria. Não foi realizada implantação nem declarada prontidão operacional de produção.
- Merge na main e envio ao GitHub não executados; merge depende de autorização explícita conforme AGENTS.md.

## Merge autorizado e entrega consolidada — 2026-10-13

- Usuário autorizou explicitamente o merge: “sim, autorizo”.
- `git switch main` e `git merge --ff-only fix/pesquisa-paginacao-pdf`: executados com sucesso. Main avançou de `c90744d` para `7c14070`, sem conflitos nem alterações no código validado. Commits funcionais `154f486`/`986ba1f` e pacote final `7c14070` integrados.
- Registro deste marco preparado na branch `docs/registro-merge-mvp-20261013` e integrado à main em avanço direto; sem correções de código adicionais. Verificar `git log -1` para o commit do registro.
- Evidências de testes anteriores preservadas; nenhuma suíte foi declarada reexecutada por causa do merge, que não mudou o conteúdo funcional. A configuração de produção foi revalidada em 2026-10-13 conforme o marco anterior.
- Nenhum push, implantação, alteração de dados persistidos ou remoção de volumes nesta etapa. Publicação no GitHub e instalação no servidor não foram executadas.
- MVP funcional consolidado na main local. Próximos updates devem ser feitos em branches próprias, com escopo definido pelo usuário; requisitos de liberação de acervo real permanecem em OPERACAO.md.

## Nova etapa — importação do acervo digitalizado — 2026-10-13

- Usuário solicitou ferramenta de migração/carga e leitura de arquivos já digitalizados, integrada ao RegistroDoc.
- Criada branch `feature/migracao-acervo-digitalizado` a partir da main consolidada, com árvore inicialmente limpa. Não alterada a main.
- Inspecionados Worker, opções do Indexer, inspector de PDF, extrator PdfPig, entidades, mapeamentos do banco e testes relacionados. Confirmado: cadastro/indexação de PDFs existente; OCR de imagens ou PDFs sem texto não implementado.
- Reaproveitamento previsto: PDF por processo, entidades atuais, texto por página, pesquisa e rotas protegidas de leitura. Saída deve usar caminho relativo à raiz comum da API.
- Solicitados formatos, organização por processo e presença de texto/OCR, com exemplos fictícios. Essas informações ainda não foram recebidas; agrupamento e tecnologia de conversão/OCR não definidos.
- Investigação e proposta registradas em MIGRACAO.md; REQUISITOS atualizado para distinguir a nova solicitação do escopo original. Nenhuma importação, acesso ao acervo real, alteração de banco ou instalação de dependência executada. Build/testes não executados nesta etapa documental; nenhuma nova funcionalidade declarada pronta.

## Importador de PDFs pesquisáveis implementado — 2026-10-13

- Usuário confirmou PDFs pesquisáveis e informou categorias/subcategorias. OCR dispensado; adotado um PDF por documento. Ano deriva da subpasta de Averbações/Habilitações/Reconhecimento de Firmas; demais categorias sem ano informado.
- Implementado `PdfBatchImporter` no Indexer existente e modos `--import-plan`/`--import-execute`; sem novo schema, pacote externo ou substituição do Worker. Carga reaproveita aquisição, estados e transação de páginas do indexador.
- Mapeamento configurável de categorias, regras específicas por subpasta, relatórios privados NDJSON, validação de texto, cópia com hash e publicação sem sobrescrita, proteção contra links, preservação de originais, retomada/detecção de duplicação e conflitos de metadados. Caminhos gravados normalizados com `/`, relativos à raiz comum da API.
- Modelos `deploy/importacao*.example.json` e `compose.import.yaml` preparados. Origem/configuração somente leitura; execução administrativa explícita. Relatórios/fixtures só em artifacts, ignorados pelo Git/Docker. O modelo aceita Livros/Lviros conforme nomes informados, a conferir na origem.
- API disponibiliza lista autenticada de tipos indexados; Web recebe filtro Categoria, preserva categoria nos links e deixa de restringir automaticamente todas as consultas a habilitações. Sem filtro, consulta os tipos já existentes; labels legíveis para as categorias informadas.
- Build final aprovado com zero avisos/erros. Suíte final em banco temporário `compose.validation.yaml`: 9 unitários e 10 de integração aprovados. Primeira tentativa de build identificou colisão do nome `Options` com namespace do projeto, corrigida; não confundir essa tentativa com o resultado final.
- Integração testada com PDF textual fictício real: simulação sem cadastro, carga, indexação de texto, pesquisa por categoria/ano, leitura do PDF, repetição sem duplicar e conflito sem alterar metadados. Testes cobrem ano ausente, PDF sem texto, categoria não mapeada, duplicação no lote e link no destino sem criar pastas externas.
- Web/API atualizados com `docker compose up -d --build --no-deps web api`; dados/volumes/indexadores preservados, sem executar importação no banco principal. HTTP e Edge aprovados, incluindo filtro de categoria.
- Imagem importer construída. `python -u tests/validar_importacao_cli.py`: aprovado com PDFs fictícios, destino privado vazio e conexão inacessível; comprova simulação sem banco/carga, ano/categoria, duplicação e bytes de origem preservados.
- Versão final valida também ancestrais das raízes para impedir aliases por links/junções; teste executado em Linux. CLI trata modos conflitantes, arquivo de configuração ausente e execução sem conexão com código 1 e mensagem sem caminhos internos; esses casos foram executados no teste do container.
- Documentação operacional e requisitos atualizados. Ferramenta disponível por terminal/Docker; painel de lotes não incluído. Carga de volume representativo, mapeamento da origem real e autorização de carga permanecem necessários; nenhum acervo real usado. Main não alterada; desenvolvimento em `feature/migracao-acervo-digitalizado`, sem merge/publicação.
- Commit de implementação: `7af7182` — importação de PDFs pesquisáveis por categoria/ano, filtro Web e verificações. Árvore de trabalho limpa após consolidação; registro deste identificador em commit documental separado.

## Validação da operação por plano revisado — 2026-10-14

- Continuidade solicitada pelo usuário. Revisados e preservados os ajustes locais preexistentes na branch `feature/migracao-acervo-digitalizado`: plano obrigatório, identificação do destino, lançador Python, proteção do contexto Docker e testes. Nenhum merge ou push nesta etapa.
- Execução exige relatório de simulação completo, sem falhas e compatível com arquivos, categorias, serventia e destino. Validação antecede conexão ao banco; o lançador confere pastas físicas, montagens somente leitura e raiz da API. O plano não substitui autorização do operador nem é assinatura digital. Falhas durante a carga podem deixar itens concluídos; retomada utiliza detecção de duplicações.
- `dotnet build RegistroDoc.slnx --no-restore`: aprovado, zero avisos e erros.
- `docker compose -f compose.validation.yaml up --abort-on-container-exit --exit-code-from tests`: aprovado, 9 unitários e 21 de integração, zero falhas/ignorados. Banco temporário separado; serviços de validação parados pelo Compose ao concluir.
- `python -B -m unittest discover -s tests -p test_importar_acervo.py -v`: 8 aprovados no Windows e 1 ignorado por permissão de criação de link. Repetição em `python:3.14-slim`, repositório montado somente leitura: 9 aprovados, zero ignorados.
- `python -u tests/validar_operacao_importacao.py`: aprovado; lançou verificação/simulação com PDF fictício, validou montagens/API e bloqueou execução sem plano ou com destino incorreto. Imagem importer reconstruída pelo lançador. Zero importações no banco principal.
- `python -u tests/validar_importacao_cli.py`: aprovado; simulação, duplicação, preservação de origem, erros de configuração, precedência do argumento de plano sobre JSON e rejeição de plano ausente/PDF modificado antes da carga. Conexão deliberadamente inacessível; zero importações.
- README e MIGRACAO atualizados com comandos do lançador, plano obrigatório, incompatibilidade de relatórios antigos e roteiro do piloto. Testes HTTP/Edge e carga não repetidos nesta etapa, que não altera Web/API.
- Etapa técnica validada com dados fictícios. Alterações permanecem locais, sem commit desta continuidade. Próximo marco operacional: definir lote piloto, conferir mapeamento/CNS e destino, preparar backup e obter autorização específica para carga. Nenhum dado real usado ou banco persistido alterado. Implantação, auditoria e carga representativa continuam pendentes.

## Simulação local do lote piloto — 2026-10-14

- Usuário definiu o diretório apresentado como lote piloto e confirmou que os próximos documentos seguirão a mesma estrutura de tipos, anos e subtipos. Autorizada a leitura/simulação; carga no banco não executada.
- Configuração local privada com 14 regras ajustadas à grafia da origem; caminhos e identificação dos arquivos não versionados. Simulação usa a serventia fictícia do ambiente local, sem validar cadastro no banco. CNS de destino deve ser confirmado antes da carga.
- `python -u tools/importar_acervo.py simular`, com variáveis locais: concluído com código 1, resultado de 416 PDFs, 411 prontos, 5 classificados como `SemTextoPesquisavel`, 0 duplicados e 0 importados. Pendências: 2 em Suprimento de Óbito e 3 em Requerimento de Mudança de Nome. Classificação do importador exige conferência dos arquivos; não foi aplicado OCR.
- Relatório completo, configuração e lista das pendências mantidos somente em área local ignorada pelo Git e Docker. Nenhum conteúdo extraído, nome documental ou caminho da origem incluído nesta documentação.
- Plano com falhas bloqueado para execução. Próximo passo: conferir/corrigir os cinco documentos ou definir explicitamente um lote que os exclua, gerar nova simulação sem falhas e obter autorização de carga com destino/CNS confirmado. Não houve cópia de PDFs para o acervo do sistema, alteração do banco, commit, merge ou push.

## Separação das pendências para OCR pelo usuário — 2026-10-14

- Usuário optou por realizar o OCR externamente e devolver os PDFs corrigidos à origem. Preparação automática de OCR interrompida; nenhuma correção automática declarada concluída.
- Cinco PDFs pendentes copiados para pasta privada `artifacts/piloto-local/para-ocr`, preservando a hierarquia relativa. SHA-256 de cada cópia conferido contra o original; originais mantidos intactos. Arquivos ignorados pelo Git e Docker.
- Aguardar devolução dos documentos com texto pesquisável. Depois, repetir a simulação completa, pois a alteração dos PDFs invalida o plano anterior. Nenhuma importação no banco executada.
- A pedido do usuário, as cinco cópias foram posteriormente movidas para a raiz da origem, sem sobrescrita e com conferência de SHA-256. Originais nas subpastas preservados. Após OCR, devolver as cópias corrigidas às posições originais e retirar as cópias de trabalho da raiz antes de nova simulação, evitando itens adicionais sem categoria.

## Continuidade do piloto sem as pendências de OCR — 2026-10-14

- Usuário informou que os cinco PDFs corrigidos estarão disponíveis no dia seguinte e pediu continuidade. Preparado lote privado separado com os 411 PDFs aprovados; cópias verificadas por SHA-256 contra o relatório anterior, preservando hierarquia e originais. As cinco pendências e suas cópias na raiz não integram esse lote.
- Nova simulação pelo lançador para o lote separado: 411 PDFs prontos, 23.032 páginas, zero duplicações, zero falhas e zero importações. Configuração, manifesto e plano mantidos somente em `artifacts/piloto-prontos`, ignorado pelo Git/Docker.
- Simulação mantém `TESTE0001` como referência; solicitada escolha do usuário entre cadastro de teste e CNS real antes de cadastrar documentos. Nenhuma alteração do banco realizada. Se o CNS mudar, gerar novo plano compatível antes da execução.
- Substituição dos cinco documentos após OCR já autorizada pelo usuário: conferir devolução, preservar cópia de segurança dos originais, substituir nos destinos mapeados e validar. Essa substituição ainda não foi executada. Carga do lote de 411 e tratamento posterior dos cinco permanecem como próximos marcos.

## Preparação do piloto com CNS informado — 2026-10-14

- Usuário informou CNS real e solicitou continuidade. Código aplicado somente à configuração privada do lote; identificação operacional não adicionada ao versionamento.
- Docker Desktop estava desligado; iniciado e conexão recuperada. Consulta somente leitura confirmou ausência do CNS informado no cadastro local e 2.023 documentos preexistentes. Solicitados nome da serventia, município e UF para cadastro correto; nenhuma serventia criada ou alterada.
- Backup do banco local criado em área privada do piloto com `pg_dump -Fc`; 423.547 bytes, SHA-256 registrado localmente e índice lido por `pg_restore --list`. Não foi executada restauração deste backup; não confundir leitura do índice com ensaio de recuperação.
- Nova simulação com o CNS informado concluída com código 0: 411 prontos, zero falhas, zero duplicações, zero importações. Plano anterior com CNS fictício não deve ser utilizado para carga.
- Próximo passo depende dos dados cadastrais solicitados. Banco e acervo da API permanecem sem carga real; cinco PDFs aguardam OCR pelo usuário. Plano, backup, configurações e documentos continuam privados e ignorados por Git/Docker. Sem commit, merge ou push.

## Ponto de retomada — carga do piloto concluída — 2026-10-15

- Usuário solicitou salvar o estado e continuar. Branch preservada: `feature/migracao-acervo-digitalizado`, com alterações locais ainda sem commit; main não alterada.
- Serventia real cadastrada e conferida com os dados fornecidos pelo usuário; UF normalizada para SE. Identificação completa permanece somente nos arquivos privados do piloto. Cadastro e carga foram autorizados na conversa e nas execuções correspondentes; não repetir cadastro nem importar o lote novamente sem necessidade.
- A carga iniciada na sessão anterior terminou enquanto o acompanhamento estava interrompido. Relatório final privado de execução: **411 importados, zero falhas, zero duplicações**. Último acompanhamento parcial tinha 263 documentos; esse número foi superado pelo fechamento do relatório, lido nesta retomada.
- Plano de 411 PDFs compreende 23.032 páginas. Conferência independente de banco, hashes, pesquisa e PDF ainda pendente neste ponto; roteiro privado preparado em `artifacts/piloto-prontos/validar_carga.py`, ainda sem resultado salvo.
- Backup anterior à carga permanece em `artifacts/piloto-prontos/backup`; índice validado e hash registrado, sem restauração executada. Configuração, plano, manifesto, cadastro e relatórios permanecem privados/ignorados. Não incluir PDFs, textos, caminhos externos ou credenciais no Git.
- Cinco documentos continuam pendentes de OCR pelo usuário. Cópias foram colocadas na raiz da origem; relação dos destinos está em `artifacts/piloto-local/pendencias.json`. Substituição autorizada após devolução: verificar os corrigidos, guardar backup dos originais e substituir nas subpastas; ainda não executada. Não importar a raiz contendo essas cópias como se fosse novo lote.
- Próxima ação: executar a conferência pós-carga e registrar resultado; depois tratar os cinco PDFs quando o usuário confirmar a devolução. Não declarar implantação de produção concluída; auditoria, HTTPS e controles operacionais continuam pendentes. Não realizar merge sem autorização explícita.

## Validação pós-carga e estado salvo para continuidade — 2026-10-15

- Executado `python -u artifacts/piloto-prontos/validar_carga.py`: resultado final aprovado. Conferidos **411 documentos concluídos, 23.032 páginas cadastradas, metadados de categoria/ano e 411 hashes dos PDFs armazenados** contra o manifesto do lote.
- Validação HTTP autenticada aprovada: pesquisa por texto, filtro por categoria/ano, paginação sem repetição, leitura de um PDF de cada uma das **13 categorias** com comparação de hash, MIME, cache e range; URLs das amostras bloqueadas após logout. Não houve homologação visual humana de todos os documentos.
- Primeira tentativa do roteiro terminou em `AttributeError` ao criar o gerenciador de cookies, após as conferências de banco/arquivos. Corrigido somente o roteiro privado e repetida a execução completa; resultado final sem falhas em `artifacts/piloto-prontos/validacao-carga.json`. Nenhuma correção no produto necessária nesta etapa.
- **Estado atual:** lote de 411 documentos importado e conferido. Não executar novamente a carga por padrão nem restaurar o backup sobre o banco atual. Dados anteriores preservados. Os cinco PDFs sem texto continuam aguardando OCR/devolução pelo usuário; não foram substituídos nem importados.
- **Retomada:** ler este último marco; verificar branch/árvore local; consultar o resultado privado da validação. Quando o usuário confirmar os cinco PDFs corrigidos na raiz da origem, conferir texto e correspondência, preservar backup dos originais, substituir nos destinos de `artifacts/piloto-local/pendencias.json` e preparar lote separado com o CNS da configuração atual de `artifacts/piloto-prontos/config.json`. A configuração antiga de `piloto-local` ainda contém o CNS fictício e não deve ser usada para carga real. Gerar/revisar nova simulação antes da carga complementar.
- Trabalho permanece na branch `feature/migracao-acervo-digitalizado`, com alterações locais sem commit, sem merge/push. Histórico de build e testes técnicos anteriores preservado; build/suítes .NET não repetidos nesta retomada operacional. Produção continua dependente dos controles de OPERACAO.md. Estado registrado a pedido do usuário; nenhum segredo ou documento privado incluído no versionamento.

## Commit, merge autorizado e próximas intervenções — 2026-10-15

- Usuário solicitou commit e merge do que está pronto, deixando os cinco PDFs em OCR para depois. Criado `3319f9f` — `Consolida importacao por plano revisado e validacao do piloto`, com código, testes, documentação e análise das próximas etapas. Dados do piloto, configurações locais, backups, credenciais e relatórios permaneceram ignorados.
- `git switch main` e `git merge --ff-only feature/migracao-acervo-digitalizado`: aprovados, avanço direto de `e6d89ef` para `3319f9f`, sem conflitos. Incluídos os commits anteriores do importador `7af7182` e `d291783`. Nenhum push ao GitHub, alteração de banco ou nova importação nesta consolidação.
- Registro posterior do merge preparado em `docs/registro-merge-importacao-20261015` para integração por avanço direto. Consultar `git log -2 --oneline` e `git status --short` para o estado final. Não executar novamente o lote por causa do merge.
- `git diff --check` e revisão dos arquivos preparados sem dados privados. Evidências existentes preservadas: build sem avisos/erros, 9 unitários + 21 integração + 9 lançador em Linux em 2026-10-14; conferência operacional dos 411 documentos e 23.032 páginas em 2026-10-15. Nenhuma suíte declarada reexecutada apenas para o commit/merge; não houve alteração funcional adicional nesta consolidação.
- Análise em [PROXIMAS-ETAPAS](PROXIMAS-ETAPAS.md): priorizar auditoria dos endpoints, depois perfis/contas, recuperação operacional, progresso de lotes e implantação/desempenho. Confirmado no código: serviço de auditoria registrado mas ainda não chamado pela pesquisa/PDF. Não foi iniciada implementação dessas intervenções.
- Próximo incremento deve usar branch própria e escopo definido. Cinco PDFs em OCR continuam aguardando devolução; integração complementar segue o procedimento dos marcos anteriores. Piloto local concluído não equivale à liberação de produção.

## Etapa 1 — auditoria de pesquisa/PDF concluída — 2026-10-15

- Implementação na branch `feature/auditoria-acessos`, criada a partir de `6702c13`. Pesquisa e arquivo agora registram tentativa antes da autorização, com usuário validado (ou nulo), ação, horário UTC e documento; resultado/status atualizados ao terminar. Sem termos pesquisados, texto de PDF, tokens ou caminhos físicos na descrição. Sem alteração de esquema.
- Política em [AUDITORIA](AUDITORIA.md): falha inicial bloqueia acesso com 503; falha final mantém tentativa iniciada e registra evento técnico 4102. Cancelamentos usam gravação independente da conexão do cliente. Cada range é uma requisição auditada; não se declara leitura humana nem entrega integral pelo simples registro. Retenção e acesso administrativo devem ser definidos para produção.
- `dotnet build RegistroDoc.slnx --no-restore`: aprovado antes do alinhamento final de dependências. A primeira execução Docker aprovou 14 unitários e 22 de integração, mas revelou conflito de versão de EF Relational no projeto unitário. Fixada referência em 10.0.12, alinhada aos projetos existentes.
- `dotnet build RegistroDoc.slnx`: restauração inicialmente bloqueada pelo sandbox (NU1301); repetida com permissão de rede e concluída sem avisos/erros.
- `docker compose -f compose.validation.yaml up --abort-on-container-exit --exit-code-from tests`: repetido após correção; saída 0, **14 unitários e 22 de integração aprovados**, sem o conflito de dependências. Banco PostgreSQL temporário e documentos fictícios. Nenhuma carga, migração ou alteração no banco do piloto; containers operacionais não reconstruídos.
- Cobertura nova: autoria de dois usuários, pesquisa, bytes de PDF completo/parcial, 206/416, 400/404, 401 ausente/inválido/expirado e 403 por política do teste, descrição sem conteúdo sensível; falha inicial/final, cancelamento e exceção. Sem nova homologação pelo navegador: a ausência de credencial representa o estado esperado após logout, cuja implementação permanece preservada. Não foram repetidos testes Python do importador, que não foi alterado.
- Entrega preparada para commit solicitado pelo usuário. Consultar `git log -1 --oneline` para o identificador final. Não houve merge, push ou ativação nos containers do piloto nesta entrega. A ativação requer integração da branch e reconstrução da API.
- **Próxima tarefa: etapa 2, perfis e contas individuais**, com critérios em [PROXIMAS-ETAPAS](PROXIMAS-ETAPAS.md). Definir matriz de permissões e responsáveis; implementar em branch própria preservando a auditoria. Cinco PDFs continuam aguardando OCR externo. Não repetir a carga dos 411 documentos.

## Etapa 2 — perfis e contas individuais concluída — 2026-10-15

- Usuário solicitou executar toda a etapa 2. Retomada encontrou implementação parcial na branch `feature/perfis-contas`, baseada em `1adf1a9`; alterações preservadas, revisadas e completadas. Matriz adotada: Consulta pesquisa/lê PDF; Operador também executa carga; Administrador gerencia contas e consulta auditoria. Detalhes e ativação em [PERFIS-CONTAS](PERFIS-CONTAS.md).
- Implementados endpoints de contas com validação, e-mail único, senha forte, transações, proteção contra autobloqueio e perda do último administrador; formulários Web protegidos por antifalsificação. Incluídas Minha conta/troca de senha, consulta paginada de auditoria e mensagem de acesso negado. Nenhuma conta real criada.
- API e cookie Web confirmam sessão no IdentityHub. Conta inativa, perfil alterado e versão de segurança divergente invalidam tokens antigos. Troca/redefinição de senha e logout revogam sessões anteriores. Logout sempre remove cookie local; avisa quando não confirma revogação remota. Bootstrap não promove nem reativa conta preexistente após reinício.
- Importador exige login de Operador/Administrador para executar e revalida antes da carga e entre arquivos. Lançador envia credenciais somente por stdin, com senha oculta; não grava credenciais em argumentos/configuração/relatórios. Simulação permanece sem login e sem acesso ao banco. Worker automático continua serviço técnico sob controle administrativo. Não houve nova carga ou substituição de PDFs.
- `dotnet build RegistroDoc.slnx`: aprovado após restauração autorizada do pacote de testes HTTP. `dotnet build RegistroDoc.slnx --no-restore`: aprovação final com **zero avisos/erros**.
- `docker compose -f compose.validation.yaml up --abort-on-container-exit --exit-code-from tests`: **14 unitários e 23 de integração aprovados**, saída 0; repetido após ajustes de sessão Web e testes de carga negada, mesmo resultado. Programas reais Web/API/IdentityHub executados por TestServer contra PostgreSQL temporário, dados exclusivamente fictícios. Cobertura inclui matriz de perfis, conta sem perfil, cadastro/formulário, 403, CSRF, autoria, cookie obsoleto, desativação/reativação, troca/redefinição de senha, downgrade, logout e permissão de carga. Não houve homologação visual humana nem medição de carga nesta etapa.
- `docker run --rm --mount type=bind,source=<repositorio>,target=/repo,readonly -w /repo python:3.14-slim python -B -m unittest discover -s tests -p test_importar_acervo.py -v`: **10 testes aprovados em Linux**, incluindo credenciais somente em stdin. Testes anteriores de simulação CLI não foram repetidos; não declarar nova execução deles.
- Corrigido Dockerfile do IdentityHub para restaurar também o projeto Contracts. `docker build -f src/RegistroDoc.IdentityHub/Dockerfile -t registrodoc-identityhub-etapa2-validation .`: aprovado, imagem separada; nenhum container operacional reiniciado. Sem novas migrações de esquema. A criação do perfil Consulta ocorrerá pelo inicializador somente quando a atualização for ativada.
- Entrega consolidada para commit na branch própria; consultar `git log -1 --oneline`. Sem merge/push e sem alterações no banco/acervo do piloto. Atualização dos serviços, cadastro de pessoas reais e integração à main não executados. Tokens emitidos pela versão anterior exigirão novo login após ativação.
- **Próxima tarefa: etapa 3, backup e recuperação operacional**, conforme [PROXIMAS-ETAPAS](PROXIMAS-ETAPAS.md). Ensaiar restauração em ambiente separado; não sobrescrever banco em uso. Cinco PDFs ainda aguardam OCR externo; não repetir a carga dos 411 documentos.

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
