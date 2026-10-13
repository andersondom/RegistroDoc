"""Teste real de expiração no Edge/IdentityHub em banco temporário separado.

Execute após construir Web/API/IdentityHub: python -u tests/validar_sessao_expirada.py
Chaves e senha aleatórias somente no ambiente dos processos/containers temporários.
Não conecta ao banco principal. Para os containers isolados ao concluir.
"""
import os
from pathlib import Path
import secrets
import subprocess
import sys


def main():
    env = dict(os.environ)
    port = env.get("SESSION_VALIDATION_PORT", "18081")
    env.update(VALIDATION_PASSWORD="Aa1!" + secrets.token_hex(24),
               VALIDATION_SIGNING_KEY=secrets.token_hex(48), WEB_PORT=port,
               IDENTITY_ADMIN_EMAIL="sessao-ficticia@example.invalid")
    env["IDENTITY_ADMIN_PASSWORD"] = env["VALIDATION_PASSWORD"]
    keys = Path(__file__).resolve().parents[1] / "artifacts" / "validation" / ("session-keys-" + secrets.token_hex(8))
    keys.mkdir(parents=True)
    env["VALIDATION_KEYS_PATH"] = str(keys)
    command = ["docker", "compose", "-f", "compose.session-validation.yaml"]

    # Os módulos leem configuração na importação; subprocesso recebe os valores isolados.
    try:
        subprocess.run(command + ["up", "-d"], env=env, check=True)
        probe = """
import http.cookiejar, time, urllib.parse, urllib.request
from validar_homologacao_http import request, credentials, antiforgery_token
deadline = time.monotonic() + 90
while time.monotonic() < deadline:
    try:
        client = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        _, _, _, body = request(client, '/login')
        email, password = credentials()
        status, url, _, body = request(client, '/auth/login', urllib.parse.urlencode(dict(email=email, password=password, __RequestVerificationToken=antiforgery_token(body))).encode())
        if status == 200 and '/pesquisa' in url and b'alert-danger' not in body:
            break
    except Exception:
        pass
    time.sleep(1)
else:
    raise SystemExit('Ambiente isolado não ficou pronto')
from validar_homologacao_navegador import main
main(session_expiry=True, restart_command=['docker', 'compose', '-f', 'compose.session-validation.yaml', 'restart', 'web'])
"""
        subprocess.run([sys.executable, "-u", "-c", probe], env=env,
                       cwd=Path(__file__).resolve().parent, check=True)
        # Backup e restauração exclusivamente no PostgreSQL deste projeto temporário.
        database = "registrodoc_session_validation"
        restored = "registrodoc_restore_validation"
        execute = command + ["exec", "-T", "postgres"]
        subprocess.run(execute + ["pg_dump", "-U", "validation", "-d", database,
                                 "-Fc", "-f", "/tmp/validation.dump"], env=env, check=True)
        subprocess.run(execute + ["createdb", "-U", "validation", restored], env=env, check=True)
        subprocess.run(execute + ["pg_restore", "-U", "validation", "-d", restored,
                                 "--exit-on-error", "/tmp/validation.dump"], env=env, check=True)
        query = 'SELECT (SELECT count(*) FROM information_schema.tables WHERE table_schema = \'public\'), (SELECT count(*) FROM "AspNetUsers"), (SELECT count(*) FROM "Serventias")'
        counts = []
        for name in (database, restored):
            result = subprocess.run(execute + ["psql", "-U", "validation", "-d", name,
                                              "-At", "-c", query], env=env, check=True,
                                    capture_output=True, text=True)
            counts.append(result.stdout.strip())
        assert counts[0] == counts[1], "Restauração não preservou tabelas e dados fictícios"
        tables, users, offices = map(int, counts[0].split("|"))
        assert tables > 10 and users == 1 and offices == 1, "Massa de backup isolado incompleta"
        print("OK: pg_dump/pg_restore em banco separado preservam tabelas, usuário e serventia fictícios", flush=True)
    finally:
        result = subprocess.run(command + ["stop"], env=env)
        if result.returncode:
            raise RuntimeError("Não foi possível parar os containers de sessão")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FALHA de validação de sessão:", type(error).__name__)
        raise SystemExit(1)
