# RegistroDoc

Sistema de Gestão Eletrônica de Documentos para serventias de Registro Civil das Pessoas Naturais (RCPN).

## Escopo do MVP

Pesquisa autenticada em habilitações de casamento digitalizadas, com **um PDF pesquisável por processo**, indexação de texto por página, filtros por texto/ano/tipo, paginação e abertura do PDF original. OCR e outros tipos de registros ficam para fases futuras.

## Componentes

- .NET 10: API, Blazor Web, IdentityHub, Worker Indexer e bibliotecas Domain/Application/Infrastructure/Contracts.
- PostgreSQL 17: metadados, texto extraído por página, histórico de indexação e auditoria.
- Docker Compose: ambiente local reproduzível.
- xUnit: testes automatizados existentes.

## Execução local (somente dados fictícios)

Pré-requisitos: Docker Desktop com Compose e Git. Configure o arquivo `.env` local com `POSTGRES_PASSWORD`, `IDENTITY_ADMIN_EMAIL`, `IDENTITY_ADMIN_PASSWORD` e `JWT_SIGNING_KEY`; **não publique esses valores**. Consulte `compose.yaml` para a configuração de serviços e portas.

```powershell
docker compose up -d --build
docker compose ps
```

Abra `http://localhost:8080` e entre com as credenciais de administrador definidas localmente. A API fica em `http://localhost:8082` e o IdentityHub em `http://localhost:8081`, ambos vinculados ao loopback. O PostgreSQL não publica porta no host.

Se a porta 8080 estiver ocupada ou reservada pelo Windows, defina `WEB_PORT=18080` no `.env` local e execute `docker compose up -d --no-deps web`. Nesse caso, abra `http://localhost:18080`. Os roteiros HTTP e de navegador usam `WEB_PORT` do ambiente ou `.env`; a comunicação interna entre containers permanece nas portas originais.

O serviço `sampledata` cria 23 PDFs fictícios funcionais e mais 2.000 PDFs fictícios de benchmark. Os indexadores processam os diretórios separados; a indexação pode levar algum tempo após o início dos containers. **Não use `docker compose down -v` sem intenção explícita de excluir o volume do banco.**

## Fluxo de validação do MVP

1. Verifique se os containers estão saudáveis e se os indexadores não apresentam erros.
2. Acesse o Web, faça login e confirme que a pesquisa exige autenticação.
3. Pesquise `GIRASSOL DOURADO` (amostra 2025) e `JASMIM AZUL` (amostra 2026 e benchmark).
4. Teste filtro por ano, ausência de resultados, paginação e abertura de PDF.
5. Confira a indexação no PostgreSQL; todos os dados de exemplo são fictícios.
6. Execute `dotnet test RegistroDoc.slnx` em ambiente autorizado, ou em contêiner de SDK, antes da entrega.

## Testes em banco isolado

Após construir a imagem da API com `docker compose build api`, execute:

```powershell
docker compose -f compose.validation.yaml up --abort-on-container-exit --exit-code-from tests
python -u tests/validar_homologacao_http.py
python -u tests/validar_homologacao_navegador.py
```

O primeiro comando executa a suíte completa em PostgreSQL temporário, separado do ambiente principal e sem portas públicas. Não usa os volumes atuais. O segundo valida o Web/API em execução na porta 8080 com dados fictícios e credenciais locais do ambiente ou `.env`, sem imprimir segredos; requer Python e não substitui conferência visual no navegador.

O terceiro requer Microsoft Edge no Windows e valida a navegação em navegador headless com perfil separado: paginação, histórico, troca de pesquisa, abertura de PDF em outra aba e logout. Capturas e perfil ficam em `artifacts/visual`, ignorado pelo Git; não substitui aprovação visual humana ou conferência da renderização do PDF.

A suíte unitária também verifica o vencimento do cookie usando relógio controlado e o handler real de autenticação: acesso antes do prazo, ausência de renovação durante o uso e redirecionamento ao login após expirar. O cookie mantém a expiração fornecida pelo IdentityHub.

Validações complementares:

```powershell
python -u tests/validar_sessao_expirada.py
python -u tests/validar_carga_http.py
python -u tests/validar_configuracao_producao.py
```

O primeiro cria Web/API/IdentityHub e PostgreSQL temporário separados, com usuário fictício e token de um minuto; verifica reinício do Web com chaves persistentes, expiração no Edge e backup/restauração de dados fictícios. Requer as imagens locais atualizadas e para os containers separados ao terminar. O segundo faz 40 consultas no Web local com quatro leitores simultâneos e grava métricas sem segredos em `artifacts/validation`. O terceiro verifica a composição de produção sem iniciar serviços nem usar segredos reais.

## Implantação no servidor local

O pacote `compose.production.yaml` prepara HTTPS direto no Web, host permitido, API/IdentityHub sem portas publicadas, chaves de sessão persistentes, acervo somente para leitura e limites de logs. Use `deploy/production.env.example` como modelo local e siga [docs/OPERACAO.md](docs/OPERACAO.md). A instalação depende do servidor, certificado e preparação autorizada do banco; ainda não foi realizada. Não iniciar os serviços de massa fictícia no servidor de produção.

## Importação de PDFs pesquisáveis

Ferramenta de lote integrada ao Indexer para copiar PDFs já pesquisáveis, extrair texto por página e cadastrá-los no acervo. Preserva originais, identifica duplicações por SHA-256, registra falhas e permite repetir a execução sem duplicar documentos. A pesquisa Web oferece filtro de categoria, incluindo os novos tipos importados.

Prepare a configuração local a partir de `deploy/importacao-categorias.example.json` e siga [docs/MIGRACAO.md](docs/MIGRACAO.md). Execute primeiro `--import-plan` para revisar o relatório; `--import-execute` é uma carga explícita e só deve ser executado contra o banco/acervo autorizado. PDFs sem texto são reportados; OCR não faz parte desta ferramenta. Origem é montada somente para leitura; relatórios ficam em diretório privado, fora do Git/Docker.

Validação da ferramenta, com PDFs exclusivamente fictícios e sem carga:

```powershell
python -u tests/validar_importacao_cli.py
```

O script requer a imagem do serviço `importer` já construída. A integração de carga/pesquisa/PDF é verificada pela suíte em banco isolado, agora com 9 testes unitários e 21 de integração.

Para operar o lote, use [tools/importar_acervo.py](tools/importar_acervo.py): ações `verificar`, `simular` e `executar --plano NOME.ndjson`. O lançador confere pastas e montagens; a execução exige um plano completo, sem falhas e compatível com os arquivos e destino atuais. As quatro variáveis `IMPORT_SOURCE_PATH`, `IMPORT_TARGET_PATH`, `IMPORT_REPORT_PATH` e `IMPORT_CONFIG_PATH` são obrigatórias no lançador. Siga o roteiro de [migração](docs/MIGRACAO.md) antes de autorizar uma carga.

Validações do lançador: `python -B -m unittest discover -s tests -p test_importar_acervo.py -v` e `python -u tests/validar_operacao_importacao.py`. O segundo usa apenas origem fictícia e simulação, sem carga no banco principal.

## Índice de pesquisa

A migração `20261007180000_AddTrigramSearchIndex` instala a extensão PostgreSQL `pg_trgm` e o índice GIN sobre `PaginasDocumento.TextoExtraido`. Consultas seletivas com `ILIKE '%termo%'` podem utilizar o índice; a escolha do plano é do PostgreSQL. Em benchmark local com 2.023 PDFs fictícios, uma consulta seletiva levou 11,074 ms com varredura sequencial e 0,523 ms com uso forçado do índice. **O segundo tempo não representa uma melhoria automática em condições normais**, pois foi medido com `enable_seqscan = off`.

## Segurança e limites atuais

**Não conectar o acervo real ainda.** O repositório é público e deve conter apenas código e exemplos fictícios; não versionar PDFs reais, dados pessoais, segredos, backups ou caminhos internos. O acesso à pesquisa e aos PDFs depende de autenticação. Login/logout validam tokens antiforgery; requisições sem token ou com token inválido são rejeitadas. Antes de produção, revisar HTTPS, sessão expirada, permissões de arquivos, retenção de logs, restauração de backup e testes de carga com documentos representativos. A massa atual repete conteúdo para benchmark e não substitui ensaio com um acervo variado de 100 mil PDFs.

## Status

MVP funcional homologado com dados fictícios e integrado à main local mediante autorização em 2026-10-13. Usuário aprovou visual e leitura do PDF. Importador e operação por plano revisado desenvolvidos na branch `feature/migracao-acervo-digitalizado`; validação de 2026-10-14: 9 testes unitários, 21 de integração e 9 do lançador aprovados em Linux. Evidências históricas de HTTP/Edge, expiração real, backup/restauração e carga moderada permanecem em [HOMOLOGACAO](docs/HOMOLOGACAO.md) e [PROGRESSO](docs/PROGRESSO.md). Pacote de produção preparado; instalação, auditoria dos acessos, configuração operacional e acervo real dependem de [OPERACAO](docs/OPERACAO.md). A etapa de importação ainda não foi integrada à main.
