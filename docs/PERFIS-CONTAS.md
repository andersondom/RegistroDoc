# Perfis e contas individuais — etapa 2

Entrega em `feature/perfis-contas`, sobre a auditoria da etapa 1. Instalação dedicada a uma serventia; não há segregação de documentos entre serventias. Não foram criadas contas reais nem alterado o banco do piloto nesta entrega.

## Matriz de permissões

| Ação | Consulta | Operador | Administrador |
|---|---|---|---|
| Pesquisar, listar tipos e abrir PDFs | Sim | Sim | Sim |
| Executar carga pelo importador | Não | Sim | Sim |
| Listar/criar/alterar/desativar contas e redefinir senhas | Não | Não | Sim |
| Consultar auditoria | Não | Não | Sim |
| Alterar a própria senha e sair | Sim | Sim | Sim |

Contas autenticadas sem um desses perfis não podem consultar documentos. As regras estão nos servidores, não apenas no menu. Verificação e simulação local de arquivos permanecem sem login: não cadastram documentos nem acessam o banco e dependem das permissões do sistema operacional.

## Uso

- **Contas:** administrador abre `/contas`, cria uma conta com e-mail único, nome, perfil e senha inicial; o padrão do formulário é Consulta. Senha exige ao menos dez caracteres, maiúscula, minúscula, número e símbolo. Entregar por canal privado. Não há envio automático de e-mail.
- Alterar perfil, nome ou situação invalida as sessões anteriores. Reativar a conta não recupera tokens antigos. Redefinir a senha também encerra sessões. Não existe exclusão de contas pela interface.
- Um administrador não pode alterar o perfil/situação da própria conta; outro administrador deve fazê-lo. A redução de privilégios é serializada no banco e protege o último administrador ativo. A troca da própria senha está disponível em **Minha conta** (`/conta`).
- **Auditoria:** `/auditorias` e `GET /api/auditorias` permitem consulta paginada somente ao Administrador. Não permitem editar/apagar eventos. Contas e eventos têm páginas de até 50 itens. Identificadores de autoria permitem relacionar eventos às contas sem registrar nomes nos eventos.
- **Cargas:** usar [o lançador](../tools/importar_acervo.py) com `executar --plano ...`. Ele solicita e-mail e senha com entrada oculta, envia as credenciais somente por stdin ao importador e não as coloca em argumentos, relatórios ou arquivos. O IdentityHub deve estar acessível no endereço de confiança configurado. Não enviar credenciais em HTTP por rede externa; o Compose usa comunicação interna entre serviços.

## Sessões e falhas

O JWT inclui a versão de segurança da conta. IdentityHub verifica conta ativa, versão e perfis atuais em cada requisição autenticada. A API confirma a sessão no IdentityHub antes de autorizar; a Web faz o mesmo ao validar o cookie. Se o serviço estiver indisponível, o acesso é bloqueado e pode ser necessário entrar novamente. Não há cache de validade que permita continuar usando um perfil removido.

Troca/redefinição de senha e logout invalidam todos os JWTs anteriores da conta. O logout remove o cookie do navegador mesmo se a revogação remota falhar; nesse caso a tela avisa que não confirmou o encerramento das outras sessões. Requisições já autorizadas/em execução não são desfeitas. O importador revalida antes da carga e entre arquivos; perda de permissão, expiração ou falha de serviço interrompe o lote, preservando itens anteriores. Retomar exige novo login e o plano revisado, com a detecção de duplicações existente.

Formulários Web de login, logout, administração e troca de senha usam proteção antifalsificação. Senhas não são retornadas pelas listagens; mensagens de falha de cadastro são genéricas. O bootstrap cria os perfis ausentes e o administrador inicial apenas se a conta ainda não existir; não promove nem reativa contas existentes a cada reinício.

## Ativação operacional e limites

1. Integrar a branch após autorização de merge. Fazer backup e verificar acesso administrativo antes de atualizar os serviços.
2. Reconstruir IdentityHub, API, Web e importer. Configurar `IdentityHub:BaseUrl` para API/importer além da Web; os arquivos Compose já fazem isso. Não há nova migração de esquema, mas o inicializador do IdentityHub cria o perfil Consulta quando necessário.
3. Entrar novamente: tokens antigos, sem versão de segurança, não são aceitos. Conferir administrador existente e cadastrar contas individuais na instalação autorizada.
4. Validar consulta, administração e carga com contas adequadas. Não repetir o piloto de 411 PDFs para testar a atualização. Os cinco PDFs em OCR continuam pendentes.

Esta entrega não reconstruiu os containers em uso nem cadastrou pessoas reais. Quem controla Docker, os arquivos da aplicação ou credenciais diretas do banco continua sendo administrador técnico e pode contornar controles da aplicação; restringir esses acessos no sistema operacional. O worker automático é serviço técnico sem sessão humana e deve permanecer sob controle administrativo. Histórico nominal de lotes, acompanhamento de progresso e auditoria de mudanças cadastrais não fazem parte da auditoria de pesquisa/PDF; o histórico operacional de cargas pertence à etapa 4.

Retenção de auditoria, backup/recuperação e HTTPS de produção continuam em [OPERACAO](OPERACAO.md). Próxima etapa: ensaio de recuperação separado, conforme [PROXIMAS-ETAPAS](PROXIMAS-ETAPAS.md).

## Evidências

[AccountAccessIntegrationTests](../tests/RegistroDoc.IntegrationTests/AccountAccessIntegrationTests.cs) inicia os programas reais da Web, API e IdentityHub com TestServer, PostgreSQL temporário e contas fictícias. Cobre perfis, conta sem perfil, cadastro e rejeições, formulários/antifalsificação, desativação/reativação, troca/redefinição de senha, downgrade, cookie obsoleto, logout, consulta de auditoria e autorização do importador. A suíte de importação verifica que a recusa ocorre antes de criar destino ou relatório de execução. O teste Python verifica que credenciais vão apenas para stdin. Não equivale a homologação visual humana nem a teste de carga. Comandos/resultados em [PROGRESSO](PROGRESSO.md).
