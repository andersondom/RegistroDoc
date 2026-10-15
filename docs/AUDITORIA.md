# Auditoria de pesquisa e PDF

Implementação da etapa 1 em `feature/auditoria-acessos`. Usa a tabela `Auditorias` existente; não exige migração de esquema. A ativação na instalação depende de integrar a branch e reconstruir a API.

## Eventos e privacidade

Os endpoints de pesquisa e arquivo do [DocumentosController](../src/RegistroDoc.Api/Controllers/DocumentosController.cs) recebem auditoria pelo [middleware](../src/RegistroDoc.Api/Services/DocumentAuditMiddleware.cs), depois da autenticação e antes da autorização. A listagem de tipos e rotas não correspondentes não estão neste escopo.

Cada requisição registra ID próprio, horário UTC de início, ação (`PesquisarDocumentos` ou `LerPdf`), usuário autenticado e ID do documento quando aplicável. Tentativas sem credencial válida têm usuário nulo; não se atribui autoria a claims de token inválido ou expirado. Identidade autenticada sem identificador válido impede acesso.

A descrição JSON contém somente versão, resultado, status HTTP e presença do cabeçalho Range. Não contém texto de PDF, termo pesquisado, URL, caminho físico, token, IP ou nome do usuário. `Parcial` indica que Range foi solicitado; o status 206 indica atendimento parcial. Cada requisição range gera um evento separado, inclusive 416. O evento não comprova leitura humana nem recebimento integral do documento.

## Falhas e resultados

1. Antes do acesso, grava `Iniciado`, sem status. Se essa gravação falhar, responde 503 com `no-store`, sem executar pesquisa ou entregar PDF, e emite o evento técnico 4101 sem detalhes da exceção.
2. Ao retornar do processamento, atualiza o mesmo registro: `RespostaConcluida`, `Negado` (401/403), `ErroHttp` (demais erros HTTP), `Cancelado` ou `ErroExecucao`. Status pode ser nulo quando o cancelamento não permite determinar a resposta. Exceções continuam propagando para o tratamento HTTP normal.
3. Cada operação de auditoria tem cancelamento próprio de cinco segundos, independente da desconexão do cliente. Se a atualização final falhar, preserva `Iniciado` e emite evento 4102 com o ID da auditoria. Não tenta substituir uma resposta que pode já ter sido enviada. Queda do processo também pode deixar registros iniciados.

Não interpretar `Iniciado` como acesso negado ou inexistente. O operador deve investigar eventos 4101/4102, disponibilidade do banco e registros iniciados antigos. Não apagar ou converter automaticamente esses registros em sucesso. Não há garantia transacional entre envio HTTP e atualização do resultado.

## Operação e continuidade

Não foi criado endpoint público de leitura ou edição de auditoria. A consulta depende de acesso administrativo ao banco. Incluir a tabela nos backups existentes; nesta etapa não há expurgo automático. Antes da produção, definir responsável, retenção, permissões administrativas e acompanhamento de falhas. Esta implementação não oferece armazenamento imutável contra administradores do banco.

A etapa 2 deve definir contas individuais e políticas para consulta, administração e carga. A auditoria preserva o identificador autenticado, mas não substitui autorização nem isolamento por serventia. O bloqueio após logout segue o fluxo Web existente; a API não introduz revogação de JWT nesta etapa.

## Validação

- Teste de integração com Kestrel, JWT e PostgreSQL isolado: autoria de dois usuários, pesquisa, PDF completo, range 206/416, 400/404, 401 por credencial ausente/inválida/expirada e 403 por política de teste; comparação dos bytes e descrição sem conteúdo sensível.
- Testes unitários: falha inicial bloqueia conteúdo, falha final preserva resposta, cancelamento independente do cliente, exceção do endpoint e rota fora do escopo.
- A chamada sem credencial verifica o estado esperado após logout; não representa uma nova homologação de logout pelo navegador. O 403 usa política exclusiva do teste, sem antecipar os perfis da etapa 2.
- Executar a suíte de integração somente no ambiente de [validação](../compose.validation.yaml), nunca apontada ao banco do piloto. Resultados e comandos efetivamente executados estão em [PROGRESSO](PROGRESSO.md).
