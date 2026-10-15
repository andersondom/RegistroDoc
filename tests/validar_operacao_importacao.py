"""Valida verificar/simular no lançador, com origem fictícia e sem carga."""
import json
import os
from pathlib import Path
import subprocess
import sys
import uuid

from validar_importacao_cli import pdf


def main():
    root = Path(__file__).resolve().parents[1]
    work = root / "artifacts" / "operacao-importacao" / uuid.uuid4().hex
    source = work / "source"
    reports = work / "reports"
    folder = source / "Habilitações" / "2025"
    folder.mkdir(parents=True)
    reports.mkdir()
    (folder / "PROCESSO-FICTICIO.pdf").write_bytes(pdf("TESTE FICTICIO DE OPERACAO SEGURA"))
    config = work / "config.json"
    config.write_text(json.dumps({"Importacao": {"SourcePath": "/source", "TargetRootPath": "/data/input",
        "ReportDirectory": "/reports", "CodigoCnsServentia": "TESTE0001", "Categorias": {
            "Habilitações": {"TipoDocumento": "HabilitacaoCasamento", "AnoPorSubpasta": True}}}}, ensure_ascii=False), encoding="utf-8")
    target = root / "data" / "input"
    assert target.is_dir(), "Acervo local de validação indisponível"
    env = dict(os.environ, IMPORT_SOURCE_PATH=str(source), IMPORT_TARGET_PATH=str(target),
               IMPORT_REPORT_PATH=str(reports), IMPORT_CONFIG_PATH=str(config), PYTHONIOENCODING="utf-8")
    command = [sys.executable, "-u", "tools/importar_acervo.py"]
    for action in ("verificar", "simular"):
        result = subprocess.run(command + [action], cwd=root, env=env, capture_output=True,
                                text=True, encoding="utf-8")
        assert result.returncode == 0 and result.stdout and "Traceback" not in result.stderr, "Lançador não concluiu verificação/simulação"
    report_files = list(reports.glob("*.ndjson"))
    assert len(report_files) == 1
    entries = [json.loads(line) for line in report_files[0].read_text().splitlines()]
    assert entries[0]["Modo"] == "Simulacao"
    assert entries[-1] == dict(Total=1, Prontos=1, Importados=0, Duplicados=0, Falhas=0)
    # Não chamar executar: teste valida que o operador precisa escolher um plano.
    result = subprocess.run(command + ["executar"], cwd=root, env=env, capture_output=True,
                            text=True, encoding="utf-8")
    assert result.returncode == 1
    wrong = work / "target-incorreto"
    wrong.mkdir()
    result = subprocess.run(command + ["verificar"], cwd=root, env=dict(env, IMPORT_TARGET_PATH=str(wrong)),
                            capture_output=True, text=True, encoding="utf-8")
    assert result.returncode == 1
    assert len(list(reports.glob("*.ndjson"))) == 1
    print("OK: lançador verifica montagens/API, simula origem fictícia e bloqueia carga sem plano/destino incorreto; zero importações")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FALHA na operação de importação:", type(error).__name__)
        raise SystemExit(1)
