"""Operação local do importador: verificar, simular e executar um plano revisado.

Lê IMPORT_SOURCE_PATH, IMPORT_CONFIG_PATH, IMPORT_REPORT_PATH e IMPORT_TARGET_PATH.
Não imprime configuração do Docker, credenciais ou caminhos do acervo.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


class ImportConfigurationError(Exception):
    pass


def overlaps(left, right):
    try:
        common = Path(os.path.commonpath([left, right]))
        return common == left or common == right
    except ValueError:  # Unidades diferentes no Windows.
        return False


def validate_host_paths(source, target, reports, config):
    paths = []
    for path in (source, target, reports):
        resolved = Path(path).resolve(strict=True)
        if not resolved.is_dir():
            raise ImportConfigurationError("Origem, destino e relatórios devem ser diretórios existentes.")
        paths.append(resolved)
    for i, left in enumerate(paths):
        for right in paths[i + 1:]:
            if overlaps(left, right):
                raise ImportConfigurationError("Origem, destino e relatórios devem ser pastas físicas separadas.")
    config = Path(config).resolve(strict=True)
    if not config.is_file() or any(overlaps(root, config) for root in paths[:2]):
        raise ImportConfigurationError("A configuração deve ficar fora da origem e do acervo.")
    return (*paths, config)


def validate_import_config(config):
    data = json.loads(config.read_text(encoding="utf-8-sig"))
    options = data.get("Importacao", {})
    if (options.get("SourcePath"), options.get("TargetRootPath"), options.get("ReportDirectory")) != (
            "/source", "/data/input", "/reports"):
        raise ImportConfigurationError("Use os caminhos internos /source, /data/input e /reports no JSON.")
    cns = options.get("CodigoCnsServentia")
    if not isinstance(cns, str) or not cns.strip() or len(cns) > 20 or cns == "CONFIGURE_CNS":
        raise ImportConfigurationError("Preencha o CNS da serventia na configuração local.")
    categories = options.get("Categorias")
    if not isinstance(categories, dict) or not categories:
        raise ImportConfigurationError("Configure as categorias da origem antes de continuar.")
    return len(categories)


def validate_private_storage(path, root=ROOT):
    try:
        relative = path.relative_to(root)
    except ValueError:
        return  # Diretório privado externo ao repositório.
    if not relative.parts or relative.parts[0] not in {"artifacts", "storage", "temp", "backups", "data"}:
        raise ImportConfigurationError("Configuração e relatórios locais devem ficar fora do repositório ou em seus diretórios privados ignorados.")


def approved_plan(reports, name):
    if not name:
        raise ImportConfigurationError("Informe --plano com o relatório de simulação revisado.")
    # Somente relatório diretamente no diretório privado montado no container.
    candidate = Path(name)
    if "/" in name or "\\" in name or candidate.name != name or candidate.suffix.lower() != ".ndjson":
        raise ImportConfigurationError("--plano aceita somente o nome do relatório .ndjson, sem caminho.")
    plan = (reports / name).resolve(strict=True)
    if plan.parent != reports or not plan.is_file():
        raise ImportConfigurationError("Plano não encontrado no diretório privado de relatórios.")
    return "/reports/" + plan.name


def compose_configuration(command, env):
    result = subprocess.run(command + ["config", "--format", "json"], cwd=ROOT, env=env,
                            capture_output=True, text=True, encoding="utf-8", check=True)
    return json.loads(result.stdout)  # Mantido apenas em memória: pode conter segredos.


def validate_api_target(configuration, target):
    api = configuration["services"]["api"]
    root = api.get("environment", {}).get("Documents__RootPath", "/data/input")
    volumes = [v for v in api.get("volumes", []) if v.get("target") == root and v.get("type") == "bind"]
    if len(volumes) != 1 or Path(volumes[0]["source"]).resolve(strict=True) != target:
        raise ImportConfigurationError("IMPORT_TARGET_PATH precisa apontar para o acervo efetivo da API.")
    if root != "/data/input":
        raise ImportConfigurationError("Este perfil exige a raiz interna /data/input na API.")


def validate_importer_mounts(configuration, source, target, reports, config):
    volumes = configuration["services"]["importer"].get("volumes", [])
    expected = {"/source": (source, True), "/data/input": (target, False),
                "/reports": (reports, False), "/config/importacao.json": (config, True)}
    if len(volumes) != len(expected):
        raise ImportConfigurationError("O perfil de importação contém montagens inesperadas.")
    for volume in volumes:
        mapping = expected.get(volume.get("target"))
        if (mapping is None or volume.get("type") != "bind" or
                Path(volume["source"]).resolve(strict=True) != mapping[0] or
                bool(volume.get("read_only", False)) != mapping[1]):
            raise ImportConfigurationError("As montagens precisam preservar origem/configuração somente para leitura e o destino validado.")


def operation_context(source, target, engine, project=""):
    # Somente a impressão digital vai para o container/relatório, sem caminhos do host.
    payload = dict(Source=os.path.normcase(str(source)), Target=os.path.normcase(str(target)),
                   SourceIdentity=(source.stat().st_dev, source.stat().st_ino),
                   TargetIdentity=(target.stat().st_dev, target.stat().st_ino), DockerEngine=engine, ComposeProject=project)
    return hashlib.sha256(json.dumps(payload, sort_keys=True).encode("utf-8")).hexdigest()


def main(argv=None):
    parser = argparse.ArgumentParser(description="Importação local de PDFs com simulação e plano revisado.")
    parser.add_argument("acao", choices=["verificar", "simular", "executar"])
    parser.add_argument("--plano", help="Nome do relatório .ndjson revisado, localizado em IMPORT_REPORT_PATH")
    parser.add_argument("--production", action="store_true", help="Inclui compose.production.yaml")
    parser.add_argument("--env-file", help="Arquivo local de ambiente do Compose")
    args = parser.parse_args(argv)
    env = dict(os.environ)
    required = ["IMPORT_SOURCE_PATH", "IMPORT_TARGET_PATH", "IMPORT_REPORT_PATH", "IMPORT_CONFIG_PATH"]
    if any(not env.get(key) for key in required):
        raise ImportConfigurationError("Defina IMPORT_SOURCE_PATH, IMPORT_TARGET_PATH, IMPORT_REPORT_PATH e IMPORT_CONFIG_PATH no ambiente.")
    source, target, reports, config = validate_host_paths(*(env[key] for key in required))
    validate_private_storage(config)
    validate_private_storage(reports)
    category_count = validate_import_config(config)
    env.update(zip(required, map(str, (source, target, reports, config))))
    command = ["docker", "compose", "--env-file", args.env_file or (".env.production" if args.production else ".env"), "-f", "compose.yaml"]
    if args.production:
        command += ["-f", "compose.production.yaml"]
    command += ["-f", "compose.import.yaml", "--profile", "importacao"]
    configuration = compose_configuration(command, env)
    validate_api_target(configuration, target)
    validate_importer_mounts(configuration, source, target, reports, config)
    if args.acao == "verificar":
        print(json.dumps(dict(Estado="AmbienteLocalValidado", CategoriasMapeadas=category_count,
                              OrigemSomenteLeitura=True, DestinoCompativelComApi=True, BancoVerificado=False)))
        return 0
    engine = subprocess.run(["docker", "info", "--format", "{{.ID}}"], env=env, capture_output=True,
                            text=True, encoding="utf-8", check=True).stdout.strip()
    project = configuration.get("name")
    if not engine or not project:
        raise ImportConfigurationError("Não foi possível identificar o Docker de destino.")
    run = command + ["run", "--build", "--rm", "--no-deps", "-e",
                     "Importacao__ExecutionContext=" + operation_context(source, target, engine, project), "importer"]
    if args.acao == "simular":
        run += ["--import-plan"]
    else:
        run += ["--import-execute", "--Importacao:ApprovedPlanPath", approved_plan(reports, args.plano)]
    print("Preparando a imagem e processando o lote configurado...", flush=True)
    result = subprocess.run(run, cwd=ROOT, env=env, capture_output=True, text=True, encoding="utf-8")
    # A saída bruta do Docker pode conter caminhos internos; publicar só o resumo conhecido.
    summary = None
    for line in result.stdout.splitlines():
        try:
            value = json.loads(line)
            if (isinstance(value, dict) and set(value) == {"Total", "Prontos", "Importados", "Duplicados", "Falhas"}
                    and all(type(count) is int and count >= 0 for count in value.values())):
                summary = value
        except json.JSONDecodeError:
            pass
    if summary is not None:
        print(json.dumps(summary))
    if result.returncode:
        print("A operação não foi concluída sem falhas. Confira a configuração, o plano revisado e o relatório privado.")
    else:
        print("Operação concluída. O relatório está no diretório privado configurado.")
    return result.returncode


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ImportConfigurationError as error:
        print(str(error))
        raise SystemExit(1)
    except KeyboardInterrupt:
        print("Cancelamento solicitado. Confira o relatório privado e o estado do container antes de retomar.")
        raise SystemExit(130)
    except Exception:
        print("Não foi possível validar o ambiente local. Confira diretórios, configuração e disponibilidade do Docker.")
        raise SystemExit(1)
