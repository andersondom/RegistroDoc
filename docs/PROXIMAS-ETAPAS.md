# Próximas intervenções — 2026-10-15

O piloto local tem 411 PDFs importados e conferidos, com 23.032 páginas. Cinco documentos aguardam OCR externo e serão tratados em lote complementar quando devolvidos. Esse pendente não impede o desenvolvimento das próximas melhorias. A instalação de produção ainda não está concluída.

## Ordem proposta

| Prioridade | Intervenção | Evidência atual | Critério de conclusão |
|---|---|---|---|
| 1 — concluída | Auditoria de pesquisa e abertura de PDF | Implementada em `feature/auditoria-acessos`, usando serviço e tabela existentes. Política e limites em [AUDITORIA](AUDITORIA.md). | Autoria, resultado, anonimato, range e falhas cobertos por testes. Ativação operacional depende de integração e reconstrução da API. |
| 2 | Perfis e contas individuais | Pesquisa/PDF exigem autenticação; o exemplo de papel Administrador está em [SecurityController](../src/RegistroDoc.Api/Controllers/SecurityController.cs). Não há política de acesso por serventia nesses endpoints. | Definir quem consulta, administra usuários e opera cargas. Aplicar políticas e testar permissões. Manter instalação dedicada a uma serventia até haver necessidade e implementação de isolamento entre serventias. |
| 3 | Backup e recuperação operacional | Há roteiro em [OPERACAO](OPERACAO.md), ensaio fictício anterior e backup anterior ao piloto com índice legível. O backup do piloto não foi restaurado em ambiente separado. | Incluir banco, PDFs e chaves; definir destino, retenção e responsável. Demonstrar recuperação em ambiente separado, sem sobrescrever o banco em uso. Agendamento persistente depende de solicitação explícita. |
| 4 | Progresso e histórico da importação | O importador grava NDJSON por item, mas [o lançador](../tools/importar_acervo.py) captura a saída e só mostra resumo ao final. O piloto exigiu leitura manual do relatório para acompanhamento. | Exibir progresso, páginas, tempo e falhas sem revelar dados pessoais; permitir consultar lotes e retomar com plano revisado. Começar pelo terminal; painel Web administrativo em etapa própria. |
| 5 | Implantação e desempenho | Compose de produção preparado; validação local não comprova HTTPS, permissões nem capacidade no servidor final. | Definir servidor/certificado, validar controles de [OPERACAO](OPERACAO.md) e medir pesquisa/PDF com carga representativa e metas de latência. |

## Próximo incremento: etapa 2

Definir com o responsável a matriz de permissões de consulta, administração de usuários e operação de cargas. Inspecionar os contratos atuais do IdentityHub, as claims emitidas e os consumidores antes de implementar políticas. Criar branch própria a partir da entrega da etapa 1; não reimportar o piloto.

Critérios: contas individuais, regras aplicadas no servidor, testes de usuário permitido/negado por operação, preservação de pesquisa/PDF e logout e autoria correta nos eventos. Manter instalação dedicada a uma serventia; não prometer isolamento entre serventias sem implementá-lo. Definir quem poderá consultar auditorias e como administrar contas sem expor credenciais.

Etapas 2 a 5 ainda não implementadas. Retenção, visibilidade administrativa e acompanhamento dos eventos devem ser definidos para produção; a etapa 1 não cria expurgo nem interface de consulta. Os cinco PDFs em OCR continuam independentes deste desenvolvimento.
