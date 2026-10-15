# Próximas intervenções — 2026-10-15

O piloto local tem 411 PDFs importados e conferidos, com 23.032 páginas. Cinco documentos aguardam OCR externo e serão tratados em lote complementar quando devolvidos. Esse pendente não impede o desenvolvimento das próximas melhorias. A instalação de produção ainda não está concluída.

## Ordem proposta

| Prioridade | Intervenção | Evidência atual | Critério de conclusão |
|---|---|---|---|
| 1 — concluída | Auditoria de pesquisa e abertura de PDF | Implementada em `feature/auditoria-acessos`, usando serviço e tabela existentes. Política e limites em [AUDITORIA](AUDITORIA.md). | Autoria, resultado, anonimato, range e falhas cobertos por testes. Ativação operacional depende de integração e reconstrução da API. |
| 2 — concluída | Perfis e contas individuais | Contas, perfis, consulta administrativa de auditoria e revogação implementados em `feature/perfis-contas`; ver [PERFIS-CONTAS](PERFIS-CONTAS.md). | Matriz aplicada no servidor, formulários protegidos e testes integrados com contas fictícias. Ativação no piloto depende de integração e atualização dos serviços. |
| 3 | Backup e recuperação operacional | Há roteiro em [OPERACAO](OPERACAO.md), ensaio fictício anterior e backup anterior ao piloto com índice legível. O backup do piloto não foi restaurado em ambiente separado. | Incluir banco, PDFs e chaves; definir destino, retenção e responsável. Demonstrar recuperação em ambiente separado, sem sobrescrever o banco em uso. Agendamento persistente depende de solicitação explícita. |
| 4 | Progresso e histórico da importação | O importador grava NDJSON por item, mas [o lançador](../tools/importar_acervo.py) captura a saída e só mostra resumo ao final. O piloto exigiu leitura manual do relatório para acompanhamento. | Exibir progresso, páginas, tempo e falhas sem revelar dados pessoais; permitir consultar lotes e retomar com plano revisado. Começar pelo terminal; painel Web administrativo em etapa própria. |
| 5 | Implantação e desempenho | Compose de produção preparado; validação local não comprova HTTPS, permissões nem capacidade no servidor final. | Definir servidor/certificado, validar controles de [OPERACAO](OPERACAO.md) e medir pesquisa/PDF com carga representativa e metas de latência. |

## Próximo incremento: etapa 3

Preparar ensaio de restauração em ambiente separado, incluindo banco, PDFs e chaves. Inventariar o backup disponível sem expor conteúdo, definir destino privado e conferir espaço. Não restaurar sobre o banco em uso nem reimportar o piloto. Definir com o responsável retenção, destino e operação de backups; agendamento persistente somente mediante solicitação explícita.

Etapas 1 e 2 implementadas e validadas em ambiente isolado. Os containers do piloto ainda precisam ser atualizados após integração autorizada. Etapas 3 a 5 permanecem pendentes. Cinco PDFs continuam aguardando OCR externo.