# Homologação funcional — RegistroDoc

Relato do usuário recebido em 2026-10-08. Resultados manuais informados, ainda não reexecutados após correções.

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
