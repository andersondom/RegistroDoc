# RegistroDoc — implantação e operação no servidor local

Pacote preparado em 2026-10-09. A homologação do MVP utiliza apenas dados fictícios. Este documento separa os controles já preparados da instalação que depende do servidor e do certificado.

## Pré-requisitos da instalação

- Servidor com Docker executando containers Linux e Compose compatível com `!reset`/`!override`. Confirmar sistema operacional, capacidade de disco, memória e endereço DNS antes de instalar.
- Certificado PFX válido para o endereço usado pelos clientes, emitido por autoridade confiável na rede. Clientes precisam confiar na cadeia; não desativar validação TLS para operação.
- Diretório do acervo e do certificado com acesso restrito. A conta da API recebe leitura; gravação de arquivos exige uma conta de ingestão separada. Não usar links simbólicos no acervo.
- Banco provisionado com as migrações da API e IdentityHub e configuração da serventia revisada. A API em Production não cria schema ou serventia fictícia automaticamente; o IdentityHub atual aplica suas migrações no início. Aprovar qualquer preparação/migração do banco antes de executar no servidor.
- Contas e política de acesso definidas. A API atual exige autenticação, mas não separa acervo por serventia/usuário; este MVP pressupõe instalação dedicada a uma serventia. O serviço de auditoria existe, mas os endpoints de pesquisa/PDF ainda não o invocam; definir e implementar a política de auditoria antes de usar acervo real.

## Configuração e inicialização

1. Copiar `deploy/production.env.example` para `.env.production` local e preencher senhas/chave aleatórias, administrador, host, diretório de PDFs e certificado. O arquivo local é ignorado por Git/Docker; não enviar seus valores para chat, logs ou repositório.
2. Conferir a composição sem imprimir sua configuração completa:

```powershell
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml config --quiet
```

3. Em servidor provisionado e autorizado, construir as imagens e iniciar somente os serviços necessários:

```powershell
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml build web api identityhub
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml up -d postgres identityhub api web
```

Não executar `up` sem a lista de serviços nesta composição: o arquivo base contém geradores e indexadores de massa fictícia. A configuração de indexação do acervo real deve ser preparada separadamente, após aprovação dos caminhos e metadados, preservando registros existentes.

O Web expõe somente HTTPS (porta externa 443 por padrão); API/IdentityHub/PostgreSQL não publicam portas. A composição restringe o host aceito, monta PDFs e certificado somente para leitura e limita os logs técnicos a três arquivos de 10 MB por serviço. HTTPS termina no Kestrel; não depende de proxy reverso.

O volume `registrodoc_webkeys` mantém as chaves de Data Protection e preserva a capacidade de ler cookies entre recriações. Protegê-lo no disco e incluí-lo no backup. Chaves de sessão não são criptografadas adicionalmente pelo pacote; a proteção do armazenamento deve ser definida no servidor. Trocar/perder as chaves invalida sessões existentes.

## Backup e recuperação

O ensaio `python -u tests/validar_sessao_expirada.py` executa `pg_dump`/`pg_restore` em bancos temporários separados e compara tabelas, usuário e serventia fictícios. Nenhum backup do banco principal foi criado nem restaurado nesta etapa.

O backup operacional precisa abranger três conjuntos: banco PostgreSQL (metadados/usuários), PDFs originais e chaves de Data Protection. Certificado e configuração secreta devem ter recuperação protegida própria. Definir frequência, retenção, destino externo ao servidor, criptografia, responsáveis e prazo tolerado de perda/indisponibilidade com o operador.

Para gerar um dump em servidor autorizado, usando arquivo temporário interno e cópia para destino privado:

```powershell
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml exec -T postgres pg_dump -U registrodoc -d registrodoc -Fc -f /tmp/registrodoc-backup.dump
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml cp postgres:/tmp/registrodoc-backup.dump ./backups/registrodoc-backup.dump
```

Criar previamente o diretório privado de backup e usar um nome único por execução. Não versionar dumps nem copiar arquivos de chave para diretórios públicos. O arquivo temporário pode ficar no container; sua retenção deve ser gerenciada pelo operador.

Ensaiar restauração em servidor/banco separado: restaurar o dump com `pg_restore --exit-on-error`, repor os PDFs e chaves com permissões corretas, e executar login/pesquisa/leitura de documentos fictícios de controle. Nunca restaurar sobre o banco em uso sem autorização e plano de recuperação. Preservar volumes; não executar comandos de remoção de bancos/volumes.

## Aceitação operacional

- Validar HTTPS e confiança do certificado em uma estação da rede, login, pesquisa e PDF; repetir vencimento da sessão e logout.
- Reiniciar/recriar somente o Web e confirmar que uma sessão ainda válida continua legível, com volume de chaves preservado.
- Validar permissões reais de arquivo, política de auditoria e acesso, retenção dos registros e backup agendado com recuperação comprovada.
- Executar carga no hardware final. `tests/validar_carga_http.py` faz 40 consultas Web/API com quatro leitores simultâneos na massa fictícia local; não representa ensaio de 100 mil PDFs ou documentos grandes. Definir meta de latência/concorrência e ensaiar acervo representativo fictício antes de liberar documentos reais.

## Limites da entrega

Configuração de produção validada estruturalmente por `tests/validar_configuracao_producao.py`; persistência de sessão após reinício do Web verificada no ambiente isolado. HTTPS com certificado real, persistência entre reinicializações no servidor, backup agendado e carga de produção dependem da infraestrutura e não foram declarados aprovados. Não foi feita instalação no servidor, migração do acervo real ou merge na main.
