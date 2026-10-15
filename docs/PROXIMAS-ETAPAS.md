# Próximas intervenções — 2026-10-15

O piloto local tem 411 PDFs importados e conferidos, com 23.032 páginas. Cinco documentos aguardam OCR externo e serão tratados em lote complementar quando devolvidos. Esse pendente não impede o desenvolvimento das próximas melhorias. A instalação de produção ainda não está concluída.

## Ordem proposta

| Prioridade | Intervenção | Evidência atual | Critério de conclusão |
|---|---|---|---|
| 1 | Auditoria de pesquisa e abertura de PDF | [AuditService](../src/RegistroDoc.Api/Services/AuditService.cs) está registrado, mas [DocumentosController](../src/RegistroDoc.Api/Controllers/DocumentosController.cs) não o utiliza. | Registrar usuário, ação, horário, identificador do documento quando aplicável e resultado. Testar autoria, acesso negado e falha de gravação. Definir o tratamento de falhas e requisições range antes da implementação. |
| 2 | Perfis e contas individuais | Pesquisa/PDF exigem autenticação; o exemplo de papel Administrador está em [SecurityController](../src/RegistroDoc.Api/Controllers/SecurityController.cs). Não há política de acesso por serventia nesses endpoints. | Definir quem consulta, administra usuários e opera cargas. Aplicar políticas e testar permissões. Manter instalação dedicada a uma serventia até haver necessidade e implementação de isolamento entre serventias. |
| 3 | Backup e recuperação operacional | Há roteiro em [OPERACAO](OPERACAO.md), ensaio fictício anterior e backup anterior ao piloto com índice legível. O backup do piloto não foi restaurado em ambiente separado. | Incluir banco, PDFs e chaves; definir destino, retenção e responsável. Demonstrar recuperação em ambiente separado, sem sobrescrever o banco em uso. Agendamento persistente depende de solicitação explícita. |
| 4 | Progresso e histórico da importação | O importador grava NDJSON por item, mas [o lançador](../tools/importar_acervo.py) captura a saída e só mostra resumo ao final. O piloto exigiu leitura manual do relatório para acompanhamento. | Exibir progresso, páginas, tempo e falhas sem revelar dados pessoais; permitir consultar lotes e retomar com plano revisado. Começar pelo terminal; painel Web administrativo em etapa própria. |
| 5 | Implantação e desempenho | Compose de produção preparado; validação local não comprova HTTPS, permissões nem capacidade no servidor final. | Definir servidor/certificado, validar controles de [OPERACAO](OPERACAO.md) e medir pesquisa/PDF com carga representativa e metas de latência. |

## Primeiro incremento recomendado

Implementar auditoria mínima em branch própria, reaproveitando serviço e tabela existentes. Não gravar texto de PDFs, termos de pesquisa em claro, tokens ou caminhos físicos na descrição do evento. Não concluir que o endpoint não foi acessado apenas porque uma resposta não foi entregue: cancelamentos, erros e múltiplas requisições de range precisam de política explícita.

Cobrir pesquisa autenticada, leitura de documento, usuário correto, logout e ausência de conteúdo sensível no evento. Para falha de auditoria, decidir se o acesso é bloqueado ou se a ocorrência será registrada por mecanismo alternativo; o serviço atual exige usuário autenticado, portanto tentativas anônimas precisam de tratamento separado.

Esta é uma análise de escopo e prioridades, não uma declaração de implementação. Políticas de retenção, visibilidade dos eventos e tratamento de falhas devem ser definidas com o responsável pela operação.
