# Homologação funcional — RegistroDoc

Histórico do relato recebido em 2026-10-08. A tabela abaixo registra o estado anterior às correções; o resultado atual consta nos marcos posteriores.

| Nº | Cenário | Resultado informado |
|---|---|---|
| 1 | Página inicial direciona para pesquisa ou login | Aprovado |
| 2 | Sem autenticação, /pesquisa exige login | Aprovado |
| 3 | Login sem erro de antiforgery | Aprovado |
| 4 | Pesquisa abre após login | Aprovado |
| 5 | Pesquisa por palavra/nome retorna documentos | Aprovado |
| 6 | Filtro por ano retorna resultados esperados | Aprovado |
| 7 | Paginação ao avançar e retornar | Falhou |
| 8 | Abrir PDF | Falhou |
| 9 | Sair encerra sessão | Aprovado |
| 10 | URL de PDF anteriormente aberto não acessível após logout | Aprovado |

Observação do usuário: depois de usar a paginação, novas pesquisas repetem os campos/filtros anteriores e não atualizam corretamente.

## Hipóteses técnicas a verificar
- Em Pesquisa.razor há rotas /pesquisa e /pesquisa/{Pagina:int}, EditForm com [SupplyParameterFromForm], _filtro e OnParametersSetAsync que chama CarregarAsync. Links de paginação constroem query string Termo/AnoReferencia, mas não foi comprovado que esses parâmetros são recuperados do endereço ao navegar.
- PesquisarAsync redefine _pagina = 1 e chama CarregarAsync, mas não necessariamente sincroniza URL/parâmetro de rota.
- A âncora Abrir PDF aponta para /documentos/{DocumentoId}/pdf; verificar se há rota Web correspondente, proxy/API, autenticação, cookies/tokens, status HTTP e content-type.
- Não assumir causa única para falhas 7 e 8 sem diagnóstico.

## Roteiro de revalidação
1. Login e acesso à pesquisa.
2. Pesquisar GIRASSOL DOURADO e JASMIM AZUL (se massa disponível).
3. Filtrar ano 2025/2026; verificar totais e conteúdo.
4. Com resultados suficientes, ir para próxima e anterior; conferir itens e filtros.
5. Na página 2, trocar termo/ano e pesquisar; confirmar retorno à página 1 e atualização de resultados.
6. Limpar filtros; pesquisar; confirmar que parâmetros anteriores desapareceram.
7. Abrir PDF na primeira página e após paginação; conferir documento correto e resposta HTTP.
8. Sair; tentar URL anterior de PDF; confirmar bloqueio.
9. Executar dotnet build e dotnet test RegistroDoc.slnx; se disponível, docker compose --profile test run --rm tests.
10. Registrar evidências, falhas, ambiente, branch e commit. Não declarar 10/10 até a execução.

## Critério de conclusão
Todos os 10 cenários aprovados na nova homologação, sem regressão de segurança, com testes automatizados relevantes executados ou limitações documentadas.

## Revalidação técnica — 2026-10-08

Branch `fix/pesquisa-paginacao-pdf`. Web/API reconstruídos; banco e volumes do ambiente preservados. Validação executada por `python -u tests/validar_homologacao_http.py`, usando credenciais locais sem imprimir credenciais, cookies ou tokens.

- Página inicial anônima direciona ao login; autenticada direciona à pesquisa. Corrigido Home.razor, que ainda continha o template inicial.
- Pesquisa anônima bloqueada; login com cookie e acesso autenticado aprovados.
- JASMIM AZUL/2026: primeira página com 20 itens, segunda com itens distintos, retorno à primeira com os mesmos documentos e links preservando termo/ano.
- Nova pesquisa GIRASSOL DOURADO/2025 retorna resultados próprios na página 1; ano incorreto retorna vazio. Formulário GET aponta para `/pesquisa`, mesmo quando exibido na página 2.
- Limpeza testada sem query string e com campos vazios enviados por GET; nenhum filtro antigo nas chamadas/links. Termo inexistente retorna vazio.
- PDFs conferidos na página 1, página 2 e ano 2025: HTTP 200, `application/pdf`, assinatura `%PDF-`, hash idêntico ao arquivo fictício indicado no resultado, `Cache-Control: no-store` e HTTP 206 para range inicial.
- Logout direciona ao login; todas as URLs de PDF conferidas ficam bloqueadas e não retornam conteúdo PDF.
- Suíte completa executada no banco temporário de `compose.validation.yaml`: 6 testes unitários e 5 de integração aprovados (há placeholders preexistentes). Build final sem avisos/erros.

**Limite:** validação HTTP não é homologação visual. Não foi executado navegador automatizado; falta conferir clique dos controles, abertura/renderização do PDF na nova aba e comportamento visual de voltar/avançar. Não declarar homologação manual 10/10 até essa conferência. Sessão expirada também permanece como verificação adicional.

## Validação adicional de segurança e navegador — 2026-10-08

- Login e logout passaram a validar antiforgery explicitamente; formulários incluem `AntiforgeryToken`. Logout exige autenticação.
- `python -u tests/validar_homologacao_http.py`: aprovado após reconstrução do Web. Além dos cenários anteriores, rejeita login/logout com token ausente ou inválido (HTTP 400), confirma sessão preservada após logout rejeitado e confirma saída com token válido.
- `python -u tests/validar_homologacao_navegador.py`: aprovado no Microsoft Edge headless com perfil próprio. Exercita formulários reais, cliques próxima/anterior, voltar/avançar, nova pesquisa na página 2 retornando à página 1, PDF em aba separada e logout com bloqueio da URL anterior.
- Pesquisa sem excesso horizontal nos viewports de 1440 e 390 px; capturas locais em `artifacts/visual`. Captura móvel inspecionada nesta etapa.
- `dotnet build RegistroDoc.slnx --no-restore`: aprovado, zero avisos e erros. Suíte em `compose.validation.yaml`: 6 unitários e 5 de integração aprovados.
- Testes unitários no Windows bloqueados por Controle de Aplicativo em duas tentativas, inclusive fora da sandbox; resultado aprovado acima corresponde à execução Linux no Docker.

Este marco substitui a ausência de navegador automatizado registrada anteriormente. Não representa homologação manual 10/10: aprovação humana do visual, renderização do PDF no visualizador e sessão expirada ainda precisam de conferência. O teste de aba verifica sua abertura na URL esperada; integridade, MIME e range do PDF são conferidos pelo roteiro HTTP.

## Expiração de sessão e revalidação — 2026-10-09

- Desativada a renovação automática do cookie para preservar o vencimento fornecido pelo IdentityHub; antes, a validade do cookie podia avançar além da validade do token da API.
- `SessionExpirationTests` usa o handler real de cookies, proteção de dados efêmera e relógio controlado. Confirma autenticação antes do vencimento, ausência de novo cookie após acesso na segunda metade da sessão e rejeição/redirecionamento ao login após expiração. Não altera relógio do sistema nem banco.
- `dotnet build RegistroDoc.slnx --no-restore`: execução final aprovada, zero avisos/erros.
- `docker compose -f compose.validation.yaml up --abort-on-container-exit --exit-code-from tests`: execução final aprovada, 7 unitários e 5 de integração. Primeira execução encontrou erro na asserção do teste novo (StringValues vazio convertido em string nula), corrigido antes da execução final.
- Web reconstruído. Windows reservou a faixa 8069–8168 e bloqueou o início na porta 8080. Configurado `WEB_PORT=18080` somente no `.env` local; Compose mantém padrão 8080 e loopback. Banco e volumes preservados.
- Roteiros HTTP e Edge reexecutados na porta 18080, aprovados. Incluem paginação, histórico, troca de filtros, integridade/range de PDF, antiforgery e bloqueio após logout.
- Expiração coberta em teste automatizado do handler; expiração em navegador com IdentityHub real e renderização do PDF no visualizador continuam fora desta execução. Aprovação manual permanece pendente.

## Fechamento funcional do MVP — 2026-10-09

- Usuário respondeu **“Visual e leitura do PDF aprovados”** após conferência em http://localhost:18080. Aprovação humana registrada separadamente dos testes automatizados; não inferir execução manual de todos os dez cenários.
- Todos os dez cenários do roteiro têm cobertura técnica aprovada nos roteiros HTTP/Edge; conferência visual e leitura do PDF foram aprovadas pelo usuário.
- `python -u tests/validar_sessao_expirada.py`: expiração real no Edge com IdentityHub configurado para um minuto em banco temporário separado, sem mudar a validade das sessões do ambiente principal. Acesso antes do prazo e após metade da duração; após vencer, pesquisa e rota de PDF direcionam ao login sem logout manual. A rota PDF usa ID fictício inexistente: HTTP 404 autenticado antes do vencimento e login depois, sem precisar de documentos reais.
- O mesmo roteiro ensaia `pg_dump`/`pg_restore` em banco separado e compara quantidade de tabelas, usuário e serventia fictícios. Não restaura sobre o banco principal.
- Execução final também reinicia o Web isolado após login e confirma que a sessão permanece válida com chaves persistentes; depois confirma o vencimento original. Chaves locais somente em artifacts/validation, ignorado pelo Git/Docker.
- Proteção adicional de PDF rejeita links simbólicos em arquivo/pastas da raiz; dois testes executados no Docker Linux. Permissões do acervo continuam necessárias para impedir alteração concorrente de arquivos entre conferência e abertura.
- Build final: zero avisos/erros. Suíte final em banco temporário: 9 unitários e 5 de integração, zero falhas.
- Roteiros HTTP/Edge aprovados na versão final do Web/API. `validar_configuracao_producao.py` aprovado: HTTPS, host permitido, serviços internos sem portas publicadas, certificados/acervo somente leitura, chaves persistentes e limites de logs.
- Carga local: 40 consultas com concorrência 4, zero falhas; 3,734 s no total, p50 232,61 ms, p95 1.585,64 ms, máximo 1.674,99 ms. Medição realizada durante outras verificações na mesma máquina; não representa meta/garantia para o servidor ou ensaio de 100 mil PDFs.

Critérios funcionais do MVP atendidos com dados fictícios e evidências acima. Implantação no servidor e requisitos de operação/auditoria/acervo real separados em OPERACAO.md. Merge na main pendente de autorização.
