"""Testes do lançador local; não executam Docker e não usam dados reais."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
from types import SimpleNamespace
import contextlib
import io

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("importar_acervo", ROOT / "tools" / "importar_acervo.py")
tool = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tool)


class ImportLauncherTests(unittest.TestCase):
    def setUp(self):
        self.work = tempfile.TemporaryDirectory(prefix="registrodoc-launcher-")
        self.addCleanup(self.work.cleanup)
        self.root = Path(self.work.name).resolve()
        self.source, self.target, self.reports = [self.root / name for name in ("source", "target", "reports")]
        for folder in (self.source, self.target, self.reports):
            folder.mkdir()
        self.config = self.root / "importacao.json"
        self.config.write_text(json.dumps({"Importacao": {"SourcePath": "/source", "TargetRootPath": "/data/input",
            "ReportDirectory": "/reports", "CodigoCnsServentia": "TESTE0001", "Categorias": {"Ficticia": {"TipoDocumento": "Ficticio"}}}}))

    def test_accepts_separate_paths_and_api_destination(self):
        self.assertEqual(tool.validate_host_paths(self.source, self.target, self.reports, self.config),
                         (self.source, self.target, self.reports, self.config))
        tool.validate_api_target({"services": {"api": {"volumes": [{"type": "bind", "target": "/data/input", "source": str(self.target)}]}}}, self.target)
        self.assertEqual(tool.validate_import_config(self.config), 1)

    def test_rejects_nested_or_same_directories(self):
        nested = self.source / "nested"
        nested.mkdir()
        for target in (self.source, nested):
            with self.assertRaises(tool.ImportConfigurationError):
                tool.validate_host_paths(self.source, target, self.reports, self.config)

    def test_rejects_different_api_destination(self):
        with self.assertRaises(tool.ImportConfigurationError):
            tool.validate_api_target({"services": {"api": {"volumes": [{"type": "bind", "target": "/data/input", "source": str(self.source)}]}}}, self.target)

    def test_requires_readonly_origin_and_configuration(self):
        volumes = [{"type": "bind", "target": name, "source": str(path), "read_only": readonly}
                   for name, path, readonly in (("/source", self.source, True), ("/data/input", self.target, False),
                                                ("/reports", self.reports, False), ("/config/importacao.json", self.config, True))]
        config = {"services": {"importer": {"volumes": volumes}}}
        tool.validate_importer_mounts(config, self.source, self.target, self.reports, self.config)
        volumes[0]["read_only"] = False
        with self.assertRaises(tool.ImportConfigurationError):
            tool.validate_importer_mounts(config, self.source, self.target, self.reports, self.config)

    def test_rejects_plan_outside_report_directory(self):
        for name in ("../outro.ndjson", "/outro.ndjson", "pasta/outro.ndjson", "pasta\\outro.ndjson", "config.json", ""):
            with self.assertRaises(tool.ImportConfigurationError):
                tool.approved_plan(self.reports, name)
        (self.reports / "plano.ndjson").write_text("ficticio")
        self.assertEqual(tool.approved_plan(self.reports, "plano.ndjson"), "/reports/plano.ndjson")

    def test_rejects_placeholder_cns(self):
        data = json.loads(self.config.read_text())
        data["Importacao"]["CodigoCnsServentia"] = "CONFIGURE_CNS"
        self.config.write_text(json.dumps(data))
        with self.assertRaises(tool.ImportConfigurationError):
            tool.validate_import_config(self.config)

    def test_rejects_private_config_in_versioned_area(self):
        with self.assertRaises(tool.ImportConfigurationError):
            tool.validate_private_storage(self.config, root=self.root)
        tool.validate_private_storage(self.root / "artifacts" / "importacao.json", root=self.root)

    def test_binds_plan_to_physical_roots_and_docker(self):
        context = tool.operation_context(self.source, self.target, "docker-ficticio")
        self.assertEqual(context, tool.operation_context(self.source, self.target, "docker-ficticio"))
        self.assertNotEqual(context, tool.operation_context(self.source, self.target, "outro-docker-ficticio"))
        self.assertNotEqual(context, tool.operation_context(self.source, self.reports, "docker-ficticio"))
        self.assertNotEqual(context, tool.operation_context(self.source, self.target, "docker-ficticio", "outro-projeto"))

    def test_resolves_host_alias_before_comparison(self):
        alias = self.root / "alias"
        try:
            alias.symlink_to(self.source, target_is_directory=True)
        except OSError:
            self.skipTest("Criação de link exige permissão neste host; repetir em Linux.")
        with self.assertRaises(tool.ImportConfigurationError):
            tool.validate_host_paths(self.source, alias, self.reports, self.config)

    def test_execution_sends_credentials_only_via_stdin(self):
        (self.reports / "plano.ndjson").write_text("ficticio")
        environment = dict(IMPORT_SOURCE_PATH=str(self.source), IMPORT_TARGET_PATH=str(self.target),
                           IMPORT_REPORT_PATH=str(self.reports), IMPORT_CONFIG_PATH=str(self.config))
        configuration = {"name": "ficticio", "services": {}}
        output = io.StringIO()
        with mock.patch.dict(tool.os.environ, environment), \
             mock.patch.object(tool, "compose_configuration", return_value=configuration), \
             mock.patch.object(tool, "validate_api_target"), mock.patch.object(tool, "validate_importer_mounts"), \
             mock.patch("builtins.input", return_value="operador@example.invalid"), \
             mock.patch.object(tool.getpass, "getpass", return_value="SenhaFicticia!123"), \
             mock.patch.object(tool.subprocess, "run", side_effect=[SimpleNamespace(stdout="engine"),
                 SimpleNamespace(stdout='{"Total":0,"Prontos":0,"Importados":0,"Duplicados":0,"Falhas":0}', returncode=0)]) as run, \
             contextlib.redirect_stdout(output):
            self.assertEqual(tool.main(["executar", "--plano", "plano.ndjson"]), 0)
        call = run.call_args
        self.assertIn("-T", call.args[0])
        self.assertNotIn("SenhaFicticia", str(call.args))
        self.assertNotIn("SenhaFicticia", str(call.kwargs["env"]))
        self.assertNotIn("SenhaFicticia", output.getvalue())
        self.assertEqual(json.loads(call.kwargs["input"])["Password"], "SenhaFicticia!123")


if __name__ == "__main__":
    unittest.main()
