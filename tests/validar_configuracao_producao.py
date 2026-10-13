"""Verifica a composição de produção sem criar containers ou usar segredos reais."""
import json
import os
from pathlib import Path
import secrets
import subprocess


def main():
    root = Path(__file__).resolve().parents[1]
    env = dict(os.environ)
    env.update(POSTGRES_PASSWORD=secrets.token_hex(24), JWT_SIGNING_KEY=secrets.token_hex(48),
               IDENTITY_ADMIN_EMAIL="producao-ficticia@example.invalid", IDENTITY_ADMIN_PASSWORD="Aa1!" + secrets.token_hex(24),
               REGISTRODOC_HOST="registrodoc.example.invalid", DOCUMENTS_PATH=str(root / "artifacts" / "acervo-ficticio"),
               TLS_CERTIFICATE_DIRECTORY=str(root / "artifacts" / "certificado-ficticio"),
               TLS_CERTIFICATE_FILE="registrodoc.pfx", TLS_CERTIFICATE_PASSWORD=secrets.token_hex(24),
               HTTPS_BIND_ADDRESS="0.0.0.0", HTTPS_PORT="443")
    result = subprocess.run(["docker", "compose", "--env-file", "deploy/production.env.example",
                             "-f", "compose.yaml", "-f", "compose.production.yaml", "config", "--format", "json"],
                            cwd=root, env=env, capture_output=True, text=True, check=True)
    config = json.loads(result.stdout)
    services = config["services"]
    for name in ("postgres", "api", "identityhub"):
        assert not services[name].get("ports"), "Serviço interno publicou porta"
    web = services["web"]
    assert len(web["ports"]) == 1 and web["ports"][0]["target"] == 8443, "Porta HTTPS incorreta"
    assert web["environment"]["ASPNETCORE_URLS"] == "https://+:8443", "Web sem HTTPS"
    assert web["environment"]["AllowedHosts"] == "registrodoc.example.invalid", "Host não restringido"
    for name in ("web", "api", "identityhub"):
        assert services[name]["environment"]["ASPNETCORE_ENVIRONMENT"] == "Production"
    for name in ("postgres", "web", "api", "identityhub"):
        assert services[name]["logging"]["options"] == {"max-size": "10m", "max-file": "3"}, "Logs sem limite"
    assert any(v["target"] == "/var/registrodoc/keys" and v["type"] == "volume" for v in web["volumes"]), "Chaves sem persistência"
    assert any(v["target"] == "/https" and v["read_only"] for v in web["volumes"]), "Certificado sem montagem somente leitura"
    assert len(services["api"]["volumes"]) == 1 and services["api"]["volumes"][0]["read_only"], "Acervo deve ser somente leitura na API"
    assert "artifacts" in (root / ".dockerignore").read_text().splitlines(), "Artefatos locais incluídos nas imagens"
    print("OK: Compose de produção válido; HTTPS, host, portas privadas, chaves persistentes, acervo somente leitura e logs limitados")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FALHA de configuração de produção:", type(error).__name__)
        raise SystemExit(1)
