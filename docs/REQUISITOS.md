# RegistroDoc — Requisitos e escopo consolidado

Atualizado em 2026-10-08. Documento de continuidade; conferir detalhes no código antes de implementar.

## Objetivo
Sistema GED para serventias de Registro Civil das Pessoas Naturais (RCPN). MVP de pesquisa autenticada em habilitações de casamento digitalizadas.

## Escopo atual documentado no README
- Um PDF pesquisável por processo.
- Indexação do texto por página.
- Pesquisa textual por palavra/nome/conteúdo.
- Filtros por ano e tipo de documento (confirmar exposição na interface).
- Paginação de resultados.
- Abertura protegida do PDF original.
- Autenticação para pesquisa e documentos.
- Indexação e metadados em PostgreSQL.
- Histórico de indexação e auditoria.
- OCR e outros tipos de registro: fases futuras, não tratar como implementados.

## Arquitetura
Solução RegistroDoc.slnx: Api, Application, Contracts, Domain, IdentityHub, Indexer, Infrastructure, Web; testes IntegrationTests e UnitTests. PostgreSQL 17 e Docker Compose. Web em http://localhost:8080; IdentityHub 8081 e API 8082 em loopback conforme README/compose. Credenciais locais por .env, jamais versionar.

## Massa de validação
README/compose descrevem 23 PDFs fictícios funcionais e 2.000 fictícios de benchmark. Expressões de exemplo: GIRASSOL DOURADO (2025) e JASMIM AZUL (2026). Não presumir que a indexação já terminou; verificar logs e banco. Não utilizar documentos reais antes de revisão de segurança e autorização.

## Critérios de qualidade
- Filtros aplicados corretamente e substituídos em nova pesquisa.
- Nova pesquisa reinicia na página 1.
- Navegação entre páginas preserva filtros atuais sem reintroduzir filtros antigos.
- Limites de página e resultados vazios tratados sem falha.
- PDF correto abre quando autenticado, inclusive após paginação.
- PDF inacessível após logout; sem vazamento por URL direta.
- Falhas da API devem produzir mensagem útil sem expor segredos.
- Testes automatizados e validação manual devem distinguir resultados comprovados de pendentes.

## Fora de escopo nesta correção
OCR, integração SCC/CRC/SIRC e expansão para outros tipos documentais. Esses são trabalhos distintos; não misturar requisitos sem autorização.

## Pendências de definição
Validar comportamento exato da navegação de PDFs (nova aba/inline/download), política de paginação/URL e cobertura de autorização de cada endpoint conforme implementação atual.
