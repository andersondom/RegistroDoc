"""Teste de simulação no executável Docker com PDFs exclusivamente fictícios.

Não cadastra dados e usa --no-deps. Execute na raiz após construir o importer.
"""
import json
import os
from pathlib import Path
import subprocess
import uuid


def pdf(text):
    content = f"BT /F1 12 Tf 50 700 Td ({text}) Tj ET"
    objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
               "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
               "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", f"<< /Length {len(content)} >>\nstream\n{content}\nendstream"]
    output = "%PDF-1.4\n"
    offsets = []
    for number, item in enumerate(objects, 1):
        offsets.append(len(output))
        output += f"{number} 0 obj\n{item}\nendobj\n"
    xref = len(output)
    output += "xref\n0 6\n0000000000 65535 f \n"
    output += "".join(f"{offset:010} 00000 n \n" for offset in offsets)
    output += f"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"
    return output.encode("ascii")


def main():
    root = Path(__file__).resolve().parents[1]
    work = root / "artifacts" / "importacao-cli" / uuid.uuid4().hex
    source = work / "source"
    report = work / "reports"
    folder = source / "Habilitações" / "2025"
    folder.mkdir(parents=True)
    report.mkdir()
    original = pdf("MIGRACAO FICTICIA GIRASSOL DE TESTE")
    (folder / "PROCESSO-001.pdf").write_bytes(original)
    (folder / "COPIA-001.PDF").write_bytes(original)
    config = work / "importacao.json"
    config.write_text(json.dumps({"Importacao": {"SourcePath": "/source", "TargetRootPath": "/data/input",
        "ReportDirectory": "/reports", "CodigoCnsServentia": "TESTE0001", "Categorias": {
            "Habilitações": {"TipoDocumento": "HabilitacaoCasamento", "AnoPorSubpasta": True}}}}, ensure_ascii=False), encoding="utf-8")
    target = work / "target"
    target.mkdir()
    env = dict(os.environ, IMPORT_SOURCE_PATH=str(source), IMPORT_CONFIG_PATH=str(config), IMPORT_REPORT_PATH=str(report), IMPORT_TARGET_PATH=str(target))
    result = subprocess.run(["docker", "compose", "-f", "compose.yaml", "-f", "compose.import.yaml",
        "--profile", "importacao", "run", "--rm", "--no-deps", "-e",
        "ConnectionStrings__RegistroDoc=Host=127.0.0.1;Port=1;Database=nao_usar;Username=nao_usar", "importer", "--import-plan"],
        cwd=root, env=env, capture_output=True, text=True)
    assert result.returncode == 0, "Executável de simulação falhou; nenhuma carga solicitada"
    summary = json.loads(result.stdout.strip().splitlines()[-1])
    assert summary == dict(Total=2, Prontos=1, Importados=0, Duplicados=1, Falhas=0)
    assert (folder / "PROCESSO-001.pdf").read_bytes() == original
    assert not list(target.iterdir()), "Simulação escreveu no destino"
    lines = report.glob("*.ndjson")
    entries = [json.loads(line) for line in next(lines).read_text().splitlines()]
    assert entries[0]["AnoReferencia"] == 2025 and entries[0]["Paginas"] == 1
    command = ["docker", "compose", "-f", "compose.yaml", "-f", "compose.import.yaml",
        "--profile", "importacao", "run", "--rm", "--no-deps", "-e", "ConnectionStrings__RegistroDoc=", "importer"]
    for args in (["--import-plan", "--import-execute"],
                 ["--import-plan", "--Importacao:ConfigPath", "/config-inexistente-ficticia.json"],
                 ["--import-execute"]):
        invalid = subprocess.run(command + args, cwd=root, env=env, capture_output=True, text=True)
        assert invalid.returncode == 1, "Configuração inválida não foi rejeitada com código 1"
        assert "/config-inexistente-ficticia.json" not in invalid.stderr, "Erro expôs caminho de configuração"
    assert not list(target.iterdir()), "Configuração inválida escreveu no destino"
    print("OK: CLI simula PDFs pesquisáveis, lê ano/categoria, detecta duplicação e preserva originais; zero importações")
    print("OK: CLI rejeita modos conflitantes, configuração ausente e carga sem conexão; erros sem caminhos internos")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FALHA na validação CLI:", type(error).__name__)
        raise SystemExit(1)
