"""Validação de navegação no Edge local; somente massa fictícia.

Execute: python tests/validar_homologacao_navegador.py
Perfis e capturas ficam em artifacts/visual, ignorado pelo Git.
Não imprime credenciais, cookies ou tokens. Não substitui aprovação humana.
"""
import base64
import json
import os
from pathlib import Path
import socket
import struct
import subprocess
import time
import urllib.parse
import urllib.request
import uuid

from validar_homologacao_http import BASE, ROOT, credentials


class DevTools:
    def __init__(self, endpoint):
        url = urllib.parse.urlparse(endpoint)
        self.sock = socket.create_connection((url.hostname, url.port), timeout=20)
        key = base64.b64encode(os.urandom(16)).decode()
        self.sock.sendall((f"GET {url.path} HTTP/1.1\r\nHost: {url.netloc}\r\n"
                           "Upgrade: websocket\r\nConnection: Upgrade\r\n"
                           f"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n").encode())
        header = b""
        while not header.endswith(b"\r\n\r\n"):
            header += self.exact(1)
        assert header.split()[1] == b"101", "Conexão DevTools recusada"
        self.seq = 0

    def exact(self, size):
        result = b""
        while len(result) < size:
            part = self.sock.recv(size - len(result))
            if not part:
                raise RuntimeError("DevTools desconectado")
            result += part
        return result

    def call(self, method, params=None):
        self.seq += 1
        data = json.dumps(dict(id=self.seq, method=method, params=params or {})).encode()
        mask = os.urandom(4)
        size = len(data)
        frame = bytes([0x81, 0x80 | (size if size < 126 else 126)])
        if size >= 126:
            frame += struct.pack("!H", size)
        self.sock.sendall(frame + mask + bytes(b ^ mask[i % 4] for i, b in enumerate(data)))
        while True:
            _, flags = self.exact(2)
            size = flags & 127
            if size == 126:
                size = struct.unpack("!H", self.exact(2))[0]
            elif size == 127:
                size = struct.unpack("!Q", self.exact(8))[0]
            mask = self.exact(4) if flags & 128 else None
            data = self.exact(size)
            if mask:
                data = bytes(b ^ mask[i % 4] for i, b in enumerate(data))
            result = json.loads(data)
            if result.get("id") == self.seq:
                assert "error" not in result, "Comando DevTools falhou"
                return result.get("result", {})

    def evaluate(self, expression):
        result = self.call("Runtime.evaluate", dict(expression=expression, returnByValue=True, userGesture=True, awaitPromise=True))
        assert "exceptionDetails" not in result, "Falha ao executar interação no navegador"
        return result.get("result", {}).get("value")

    def wait(self, expression):
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            if self.evaluate(expression):
                return
            time.sleep(0.1)
        raise AssertionError("Navegação não terminou no prazo")


def main(session_expiry=False, restart_command=None):
    edge = Path(os.environ.get("PROGRAMFILES(X86)", "C:/Program Files (x86)")) / "Microsoft/Edge/Application/msedge.exe"
    assert edge.exists(), "Microsoft Edge não encontrado"
    output = ROOT / "artifacts" / "visual" / ("homologacao-" + uuid.uuid4().hex)
    output.mkdir(parents=True)
    process = subprocess.Popen([str(edge), "--headless=new", "--disable-gpu", "--no-first-run",
                                "--remote-debugging-port=0", "--remote-debugging-address=127.0.0.1",
                                "--user-data-dir=" + str(output / "profile"), BASE + "/pesquisa"],
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    dev = None
    try:
        port_file = output / "profile" / "DevToolsActivePort"
        deadline = time.monotonic() + 30
        while not port_file.exists() and time.monotonic() < deadline:
            time.sleep(0.1)
        assert port_file.exists(), "Edge não iniciou DevTools"
        port = int(port_file.read_text().splitlines()[0])
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

        def targets():
            with opener.open(f"http://127.0.0.1:{port}/json", timeout=10) as response:
                return json.load(response)

        target = next(t for t in targets() if t["type"] == "page")
        dev = DevTools(target["webSocketDebuggerUrl"])
        dev.wait("location.pathname === '/login' && !!document.querySelector('#email')")
        email, password = credentials()
        dev.evaluate("document.querySelector('#email').value=" + json.dumps(email) + ";document.querySelector('#senha').value=" + json.dumps(password) + ";document.querySelector('form[action=\"/auth/login\"]').requestSubmit()")
        dev.wait("location.pathname === '/pesquisa' && !!document.querySelector('#termo')")

        if session_expiry:
            assert not dev.evaluate("!!document.querySelector('.alert-danger')"), "API isolada indisponível"
            if restart_command:
                subprocess.run(restart_command, cwd=ROOT, check=True)
                deadline = time.monotonic() + 20
                while time.monotonic() < deadline:
                    try:
                        with opener.open(BASE + "/login", timeout=2) as response:
                            if response.status == 200:
                                break
                    except OSError:
                        pass
                    time.sleep(.2)
                else:
                    raise AssertionError("Web não ficou pronto após reinício")
                dev.call("Page.navigate", dict(url=BASE + "/pesquisa"))
                dev.wait("location.pathname === '/pesquisa' && !!document.querySelector('#termo')")
                assert not dev.evaluate("!!document.querySelector('.alert-danger')"), "Sessão falhou após reiniciar Web"
                print("OK: sessão válida preservada após reinício do Web com chaves persistentes", flush=True)
            pdf = "/documentos/99999999-9999-4999-8999-999999999999/pdf"
            status = dev.evaluate("fetch(" + json.dumps(pdf) + ").then(r=>r.status)")
            assert status == 404, "Rota protegida não alcançou API antes da expiração"
            print("OK: sessão real autenticada e API acessível; aguardando vencimento de um minuto", flush=True)
            time.sleep(35)
            dev.call("Page.navigate", dict(url=BASE + "/pesquisa"))
            dev.wait("location.pathname === '/pesquisa' && !!document.querySelector('#termo')")
            assert not dev.evaluate("!!document.querySelector('.alert-danger')"), "Sessão falhou antes do vencimento"
            print("OK: acesso após metade do prazo; aguardando expiração sem logout", flush=True)
            time.sleep(30)
            for path in ("/pesquisa", pdf):
                dev.call("Page.navigate", dict(url=BASE + path))
                dev.wait("location.pathname === '/login' && !!document.querySelector('#email')")
                assert not dev.evaluate("!!document.querySelector('article.card')"), "Resultados expostos após expiração"
            print("OK: sessão expirada no Edge bloqueia pesquisa e rota PDF, sem logout", flush=True)
            return

        def search(term, year):
            dev.evaluate("document.querySelector('#termo').value=" + json.dumps(term) + ";document.querySelector('#ano').value=" + json.dumps(year) + ";document.querySelector('form[action=\"/pesquisa\"]').requestSubmit()")
            dev.wait("location.pathname === '/pesquisa' && new URLSearchParams(location.search).get('Termo') === " + json.dumps(term) + " && document.querySelector('#termo')?.getAttribute('value') === " + json.dumps(term) + " && !!document.querySelector('article.card')")

        def ids():
            return dev.evaluate("Array.from(document.querySelectorAll('article.card a')).map(a=>a.pathname)")

        search("JASMIM AZUL", "2026")
        first = ids()
        assert len(first) == 20, "Massa de paginação insuficiente"
        dev.evaluate("document.querySelector('nav[aria-label=\"Paginação dos resultados\"] a:last-child').click()")
        dev.wait("location.pathname === '/pesquisa/2' && document.body.innerText.includes('Página 2 de')")
        assert not set(first) & set(ids()), "Próxima página repetiu resultados"
        dev.evaluate("document.querySelector('nav[aria-label=\"Paginação dos resultados\"] a:first-child').click()")
        dev.wait("location.pathname === '/pesquisa/1' && document.body.innerText.includes('Página 1 de')")
        assert ids() == first, "Anterior alterou documentos"
        dev.evaluate("history.back()")
        dev.wait("location.pathname === '/pesquisa/2' && document.body.innerText.includes('Página 2 de')")
        dev.evaluate("history.forward()")
        dev.wait("location.pathname === '/pesquisa/1' && document.body.innerText.includes('Página 1 de')")
        dev.evaluate("document.querySelector('nav[aria-label=\"Paginação dos resultados\"] a:last-child').click()")
        dev.wait("location.pathname === '/pesquisa/2' && document.body.innerText.includes('Página 2 de')")
        search("GIRASSOL DOURADO", "2025")
        assert not set(first) & set(ids()), "Nova pesquisa conservou documentos anteriores"
        print("OK: cliques próxima/anterior, histórico e nova pesquisa após paginação")

        for width, mobile in ((1440, False), (390, True)):
            dev.call("Emulation.setDeviceMetricsOverride", dict(width=width, height=900, deviceScaleFactor=1, mobile=mobile))
            assert dev.evaluate("document.documentElement.scrollWidth <= innerWidth"), "Pesquisa excede largura da tela"
            shot = dev.call("Page.captureScreenshot", dict(format="png", captureBeyondViewport=True))
            (output / f"pesquisa-{width}.png").write_bytes(base64.b64decode(shot["data"]))
        print("OK: pesquisa sem excesso horizontal em desktop e celular; capturas locais")
        pdf = ids()[0]
        dev.evaluate("document.querySelector('article.card a').click()")
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            if any(t["id"] != target["id"] and pdf in t["url"] for t in targets()):
                break
            time.sleep(0.1)
        else:
            raise AssertionError("PDF não abriu em outra aba")
        print("OK: clique de PDF abre outra aba (conteúdo conferido pelo roteiro HTTP)")
        dev.evaluate("document.querySelector('form[action=\"/auth/logout\"]').requestSubmit()")
        dev.wait("location.pathname === '/login' && !!document.querySelector('#email')")
        dev.call("Page.navigate", dict(url=BASE + pdf))
        dev.wait("location.pathname === '/login' && !!document.querySelector('#email')")
        print("OK: logout pelo formulário e bloqueio de URL anterior do PDF")
    finally:
        if dev:
            try:
                dev.call("Browser.close")
            except (OSError, RuntimeError):
                pass
            dev.sock.close()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.terminate()
            process.wait(timeout=10)


if __name__ == "__main__":
    try:
        main()
    except AssertionError as error:
        print("FALHA:", str(error))
        raise SystemExit(1)
    except Exception as error:
        print("FALHA de execução:", type(error).__name__)
        raise SystemExit(1)
