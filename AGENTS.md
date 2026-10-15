# Instruções permanentes — RegistroDoc

## Missão
Continuar o GED RegistroDoc existente para serventias de Registro Civil, sem recriar a solução. Comunicar-se com o usuário em português do Brasil, de modo objetivo, orientando decisões técnicas e registrando o progresso.

## Fontes de verdade
1. Código atual do repositório e testes executados.
2. README.md, compose.yaml e RegistroDoc.slnx.
3. docs/REQUISITOS.md e docs/HOMOLOGACAO.md: histórico e critérios; não confundir relatos de homologação com testes automatizados.
4. docs/PROGRESSO.md: pendências e próximos passos. Atualizar após alterações relevantes.

## Arquitetura conhecida
.NET 10; Blazor Web com InteractiveServer; API; IdentityHub; Indexer; bibliotecas Domain, Application, Infrastructure, Contracts; PostgreSQL 17; Docker Compose; xUnit. Verificar sempre o código atual antes de supor contratos e endpoints.

## Regras de execução
- Trabalhar sobre o código existente. Antes de editar, inspecionar chamadas, estado, autenticação e testes associados.
- Criar branch específica para correções; não realizar merge na main sem aprovação explícita.
- Preferir alterações pequenas, revisáveis e acompanhadas de testes de regressão.
- Executar build e testes disponíveis; registrar comandos, resultados e limitações. Nunca declarar um teste aprovado sem executá-lo.
- Não introduzir dados reais do cartório. Repositório público: somente amostras fictícias.
- Não publicar .env, credenciais, JWT signing keys, PDFs reais, backups, dados pessoais ou caminhos internos.
- Não executar docker compose down -v, docker volume rm, exclusão de bancos/volumes, resets destrutivos ou operações equivalentes.
- Não executar migrações destrutivas nem alterar dados persistidos sem autorização explícita.
- Preservar autenticação e autorização para pesquisa/PDF, incluindo bloqueio após logout.
- Se uma ação exigir acesso local, permissão ou dados ausentes, pedir somente o necessário.
- Atualizar docs/PROGRESSO.md após cada marco; se regras permanentes mudarem, atualizar este arquivo.

## Próxima tarefa prioritária
O fluxo de pesquisa/PDF e o lote piloto de 411 PDFs foram validados. Consultar o último marco de docs/PROGRESSO.md antes de operar o banco; não repetir a carga por padrão. Os cinco documentos em OCR aguardam devolução pelo usuário. Auditoria de pesquisa/PDF implementada na branch feature/auditoria-acessos; consultar docs/AUDITORIA.md para política e limites. Próxima intervenção: etapa 2, perfis e contas individuais, conforme docs/PROXIMAS-ETAPAS.md. Integração e ativação operacional da auditoria devem ser verificadas antes de supor que os containers em uso já executam a entrega. Implementar novos incrementos em branches próprias; não alterar a main diretamente. Dados do piloto ficam somente nas áreas privadas ignoradas, nunca no conteúdo versionado.
