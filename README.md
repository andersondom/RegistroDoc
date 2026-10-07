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

O serviço `sampledata` cria 23 PDFs fictícios funcionais e mais 2.000 PDFs fictícios de benchmark. Os indexadores processam os diretórios separados; a indexação pode levar algum tempo após o início dos containers. **Não use `docker compose down -v` sem intenção explícita de excluir o volume do banco.**

## Fluxo de validação do MVP

1. Verifique se os containers estão saudáveis e se os indexadores não apresentam erros.
2. Acesse o Web, faça login e confirme que a pesquisa exige autenticação.
3. Pesquise `GIRASSOL DOURADO` (amostra 2025) e `JASMIM AZUL` (amostra 2026 e benchmark).
4. Teste filtro por ano, ausência de resultados, paginação e abertura de PDF.
5. Confira a indexação no PostgreSQL; todos os dados de exemplo são fictícios.
6. Execute `dotnet test RegistroDoc.slnx` em ambiente autorizado, ou em contêiner de SDK, antes da entrega.

## Índice de pesquisa

A migração `20261007180000_AddTrigramSearchIndex` instala a extensão PostgreSQL `pg_trgm` e o índice GIN sobre `PaginasDocumento.TextoExtraido`. Consultas seletivas com `ILIKE '%termo%'` podem utilizar o índice; a escolha do plano é do PostgreSQL. Em benchmark local com 2.023 PDFs fictícios, uma consulta seletiva levou 11,074 ms com varredura sequencial e 0,523 ms com uso forçado do índice. **O segundo tempo não representa uma melhoria automática em condições normais**, pois foi medido com `enable_seqscan = off`.

## Segurança e limites atuais

**Não conectar o acervo real ainda.** O repositório é público e deve conter apenas código e exemplos fictícios; não versionar PDFs reais, dados pessoais, segredos, backups ou caminhos internos. O acesso à pesquisa e aos PDFs depende de autenticação. Antes de produção, revisar HTTPS, proteção CSRF dos endpoints de login/logout, permissões de arquivos, retenção de logs, restauração de backup e testes de carga com documentos representativos. A massa atual repete conteúdo para benchmark e não substitui ensaio com um acervo variado de 100 mil PDFs.

## Status

MVP em validação final. Indexação, pesquisa, paginação e visualização de PDF foram validadas manualmente com dados fictícios. Testes automatizados e revisão de segurança devem ser confirmados antes de considerar o sistema pronto para produção.
