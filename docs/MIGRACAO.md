# Importação do acervo digitalizado

Estado atual: importador de PDFs pesquisáveis implementado, com operação por plano revisado. As seções iniciais preservam a investigação histórica; as instruções de operação abaixo refletem o fluxo atual. OCR e painel Web de lotes não fazem parte desta entrega.

Solicitação recebida em 2026-10-13: criar ferramenta integrada ao RegistroDoc para carregar arquivos já digitalizados e ler seu conteúdo. Desenvolvimento na branch `feature/migracao-acervo-digitalizado`; nenhuma importação realizada nesta etapa.

## Situação comprovada

- O Indexer monitora uma pasta configurada, trabalha com PDFs e calcula SHA-256 para evitar duplicação de conteúdo.
- `PdfPigTextExtractor` extrai somente texto presente nas páginas do PDF. Não há OCR implementado; uma imagem escaneada sem camada de texto não se torna pesquisável por esse extrator.
- O cadastro atual contém serventia, tipo, ano, caminho relativo, hash, páginas e estado de indexação; o texto fica em `PaginasDocumento`, utilizado pela pesquisa da API.
- As rotas de leitura servem PDF autenticado. A ferramenta deve produzir ou preservar um PDF por processo para reutilizar esse fluxo, conservando os arquivos de origem.
- O caminho de saída precisa ser relativo à raiz comum do acervo utilizada pela API. Não repetir a divergência histórica entre raízes dos indexadores e raiz de leitura.

## Definições necessárias antes da conversão

- Formatos de origem: PDF, JPG/JPEG, PNG, TIFF ou combinação; verificar TIFF multipágina se houver.
- Unidade documental: um arquivo por processo ou várias imagens/páginas agrupadas por pasta ou nome.
- Regra de ordem das páginas, identificação do processo e origem do ano/tipo. Não inferir esses dados de nomes reais sem regra validada.
- Presença de texto pesquisável e necessidade de OCR; idioma, legibilidade e materiais manuscritos a avaliar em amostras fictícias.

Essas definições foram solicitadas ao usuário; ainda não foram fornecidas. Escolha do motor de OCR e contratos finais dependem delas.

## Fluxo proposto para implementação

1. Inventariar a origem em modo de simulação: formatos, agrupamentos, páginas e pendências, sem mover/excluir arquivos ou cadastrar documentos.
2. Validar agrupamento, metadados e ordenação antes da carga; apresentar os casos ambíguos para correção.
3. Preservar originais e gerar PDFs derivados em área de trabalho separada quando necessário. Extrair texto existente e aplicar OCR somente conforme o formato/política definidos.
4. Validar o resultado, calcular hash e publicar o PDF completo na raiz comum antes de encaminhar para cadastro/indexação. Reutilizar as entidades e pesquisa existentes.
5. Registrar progresso e erros por item, permitir retomada e evitar duplicações em nova execução. Não declarar sucesso se o texto não foi lido ou o PDF ficou incompleto.
6. Disponibilizar operação administrativa integrada, com fontes configuradas no servidor e autorização apropriada; não aceitar acesso irrestrito a caminhos enviados pelo navegador.

O fluxo é proposta técnica, não funcionalidade já implementada. Não foi escolhida biblioteca, executável ou migração de banco nesta etapa.

## Validação planejada

Somente amostras fictícias e banco isolado. Cobrir agrupamento/ordem, texto extraído ou OCR, arquivos corrompidos, retomada, duplicação, cancelamento, preservação dos originais, limites de caminho e integração até pesquisa/abertura protegida do PDF. A carga do acervo real e alterações no banco persistido exigem autorização específica.

## Implementação e estrutura confirmadas — 2026-10-13

O usuário confirmou PDFs pesquisáveis organizados por categoria; não é necessário OCR. A implementação utiliza um PDF por documento e operação administrativa de lote no Indexer existente, integrada ao banco, à pesquisa e à leitura autenticada. Os itens abaixo substituem as definições pendentes da investigação inicial.

| Pasta de origem | Categoria no sistema | Ano |
|---|---|---|
| Averbações/AAAA | Averbações | Subpasta obrigatória de quatro dígitos |
| Habilitações/AAAA | Habilitações | Subpasta obrigatória de quatro dígitos |
| Reconhecimento de Firmas/AAAA | Reconhecimento de Firmas | Subpasta obrigatória de quatro dígitos |
| Nascimento e Óbito/Adoção | Adoção | Não informado |
| Nascimento e Óbito/Averbações | Averbações de Nascimento e Óbito | Não informado |
| Nascimento e Óbito/Declaração de Nascimento | Declaração de Nascimento | Não informado |
| Nascimento e Óbito/Declaração de Óbito | Declaração de Óbito | Não informado |
| Nascimento e Óbito/Livros | Livros de Nascimento e Óbito | Não informado |
| Nascimento e Óbito/Protocolos de Nascimento e Óbito | Protocolos de Nascimento e Óbito | Não informado |
| Nascimento e Óbito/Reconhecimento de Paternidade | Reconhecimento de Paternidade | Não informado |
| Nascimento e Óbito/Suprimento de Assento Nascimento | Suprimento de Assento Nascimento | Não informado |
| Nascimento e Óbito/Suprimento de Óbito | Suprimento de Óbito | Não informado |
| Reconhecimento de Mudança de Gênero | Reconhecimento de Mudança de Gênero | Não informado |
| Requerimento de Mudança de Nome | Requerimento de Mudança de Nome | Não informado |

O modelo `deploy/importacao-categorias.example.json` também aceita a grafia `Lviros` informada na conversa. Ajustar os nomes ao diretório real; dentro do container Linux a correspondência considera maiúsculas/minúsculas e acentos. Uma regra mais específica prevalece sobre outra com prefixo mais curto. O ano não é inferido do nome do PDF ou de suas propriedades.

## Preparação da carga

1. Copiar o modelo para configuração local fora do versionamento (por exemplo, `artifacts/importacao/config/importacao.json`). Preencher `CodigoCnsServentia` com a serventia **ativa e já cadastrada**. Nenhuma serventia é criada automaticamente.
2. Definir as variáveis `IMPORT_SOURCE_PATH`, `IMPORT_CONFIG_PATH` e `IMPORT_REPORT_PATH` com caminhos absolutos locais, e preparar o diretório privado de relatórios. A origem e a configuração são montadas somente para leitura.
3. `IMPORT_TARGET_PATH` tem padrão `./data/input`. Para outro acervo, defini-lo com o mesmo diretório usado pela API. Os caminhos internos do modelo são `/source`, `/data/input` e `/reports`; os caminhos gravados no banco são relativos à raiz comum da API.
4. Conferir o modelo e executar simulação antes da carga. O modo de simulação lê os PDFs e gera relatório, sem copiar PDFs para o destino ou acessar o banco. Não valida a existência da serventia, pois isso é feito somente na execução.

```powershell
docker compose -f compose.yaml -f compose.import.yaml --profile importacao build importer
docker compose -f compose.yaml -f compose.import.yaml --profile importacao run --rm --no-deps importer --import-plan
```

Depois de revisar o relatório e autorizar a carga no banco/acervo escolhido:

```powershell
docker compose -f compose.yaml -f compose.import.yaml --profile importacao run --rm --no-deps importer --import-execute --Importacao:ApprovedPlanPath /reports/NOME-DO-PLANO-REVISADO.ndjson
```

O banco já deve estar ativo e provisionado. O importador não executa migrações de schema. Em produção, usar também o arquivo de ambiente e as sobreposições correspondentes ao servidor, conferindo origem/destino antes de executar.

Os mesmos modos podem ser usados com `dotnet run --project src/RegistroDoc.Indexer -- --import-plan --Importacao:ConfigPath caminho-config-local`, ou `--import-execute --Importacao:ApprovedPlanPath caminho-do-plano`. Nesse caso, configurar caminhos do host no JSON e fornecer a conexão por variável `ConnectionStrings__RegistroDoc` tanto na simulação quanto na execução: sua identidade faz parte do plano, embora a simulação não acesse o banco. Nunca colocar credenciais no comando ou no arquivo versionado.

## Operação assistida por terminal — 2026-10-14

O lançador [importar_acervo.py](../tools/importar_acervo.py) verifica as pastas físicas, as montagens somente leitura e a correspondência entre destino e raiz da API. Requer Python, Docker e as quatro variáveis `IMPORT_SOURCE_PATH`, `IMPORT_TARGET_PATH`, `IMPORT_REPORT_PATH` e `IMPORT_CONFIG_PATH` explicitamente definidas. As pastas devem existir e estar separadas; configuração e relatórios devem ficar em área privada, fora do versionamento.

```powershell
python tools/importar_acervo.py verificar
python tools/importar_acervo.py simular
```

Revisar o arquivo `.ndjson` gerado na pasta privada: categorias, anos, contagens, duplicações e falhas. A simulação não verifica cadastro da serventia nem conflitos com documentos já existentes no banco. Com autorização específica para o lote e o destino, executar:

```powershell
python tools/importar_acervo.py executar --plano NOME-DO-PLANO-REVISADO.ndjson
```

Em produção, usar `--production --env-file CAMINHO-DO-AMBIENTE-LOCAL` em todas as ações. O lançador inclui a sobreposição de produção, sem iniciar banco/API nem aplicar migrações.

O plano deve estar completo e sem falhas, com ao menos um PDF pronto. Alterações nos arquivos, categorias, serventia, destino ou identidade da conexão exigem nova simulação. Pelo lançador, o plano também fica vinculado às pastas físicas, ao Docker e ao projeto Compose. A execução verifica o lote antes de acessar o banco; erros posteriores podem deixar itens já importados, portanto não há transação única de lote. A retomada usa a detecção de duplicações existente. Não editar o relatório para contornar a validação: ele registra a revisão operacional, não constitui assinatura digital nem controle de autorização do operador.

Relatórios antigos sem cabeçalho de versão não são aceitos como planos. Gerar uma nova simulação com a versão atual e usar o mesmo meio de operação para simular e executar.

Para cada piloto, definir uma pasta contendo somente o lote autorizado, conferir o mapeamento e a serventia, preparar backup e revisar a simulação. Após a carga, conferir contagens, pesquisa por categoria/ano e abertura autenticada de PDFs. O piloto local de 411 PDFs foi concluído e conferido em 2026-10-15, conforme [PROGRESSO](PROGRESSO.md); cinco PDFs aguardam OCR externo. A instalação no servidor permanece pendente.

## Comportamento e relatórios

- Preserva bytes e localização da origem. Extrai o texto existente por página e não usa OCR.
- Valida texto pesquisável antes de cadastrar. PDF sem texto, arquivo corrompido, categoria não mapeada ou subpasta de ano inválida é reportado como falha.
- Copia para `importacao/prefixo-do-hash/hash/nome-original.pdf`, verifica SHA-256 e publica por renomeação de arquivo temporário. Não sobrescreve arquivo existente nem apaga originais.
- Reaproveita aquisição, transação de páginas e estados do Worker. Nova execução evita duplicar documentos; registros concluídos só são aceitos quando o PDF continua disponível e com hash correto na raiz de leitura.
- Mesmo conteúdo associado a categorias, anos ou serventias diferentes gera conflito, sem alterar metadados existentes. O índice de hash atual identifica conteúdo globalmente; arquivos byte a byte idênticos são tratados como duplicações de conteúdo.
- Usa ordem estável por caminho relativo. Não segue links simbólicos na origem; bloqueia links no destino antes de criar subpastas e rejeita links/junções nos ancestrais das raízes configuradas. Origem/destino não podem estar aninhados entre si, e relatórios devem ficar separados. Em Docker, conferir também que as pastas do host montadas como origem/destino são fisicamente distintas; a comparação das raízes internas não detecta dois bind mounts do mesmo diretório.
- Grava um relatório privado `.ndjson` por execução, com estado por arquivo e resumo final. Faz flush por item, preservando o relatório parcial em interrupção. Relatórios contêm nomes relativos/categorias/hashes e devem ter acesso restrito; não contêm texto extraído, senha ou exceção interna.
- Retorna código 0 sem falhas, 1 quando há falhas, 130 no cancelamento por Ctrl+C. Itens `IndexacaoPendente` ou conflitantes não são declarados importados.
- Execução sequencial, com extração de um PDF por vez; o inventário de caminhos e a lista de hashes ficam em memória. Carga de grande volume ainda requer ensaio representativo e acompanhamento de disco/memória. Relatórios ajudam a acompanhar progresso; esta entrega não inclui painel de execução de lotes.

Na interface Web, o campo Categoria lista os tipos já indexados, conserva o filtro na paginação e permite consultar todas as categorias. Categorias ainda sem documentos concluídos não aparecem na lista.

## Verificação executada

- Build final com zero avisos/erros.
- Suíte completa em `compose.validation.yaml`: 9 unitários e 10 de integração aprovados. Os testes novos usam PDFs textuais fictícios, passando por PdfPig, cadastro/indexação, consulta da API, leitura do PDF, retomada e conflitos. Incluem simulação sem banco, ano ausente, destino com link e raiz com ancestral simbólico reais no Docker Linux.
- `python -u tests/validar_importacao_cli.py`: aprovado; executável Docker em simulação, origem/destino fictícios e banco inacessível. Categoria/ano, texto por página, duplicação e preservação de origem conferidos; zero importações. Também rejeita modos conflitantes, configuração ausente e execução sem conexão, com código 1 e sem exibir caminhos internos.
- Roteiros HTTP e Edge aprovados após atualização Web/API; HTTP verifica categoria válida/inexistente e Edge verifica seleção do filtro pelo formulário.
- Nenhum PDF real carregado, nenhuma execução de importação no banco principal e nenhuma migração de schema. Validar o mapeamento e autorizar separadamente a carga do acervo real.
