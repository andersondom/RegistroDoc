"""Validação HTTP local com massa fictícia; não imprime credenciais ou cookies.

Execute após atualizar Web/API: python tests/validar_homologacao_http.py
Usa IDENTITY_ADMIN_EMAIL/PASSWORD do ambiente ou .env local.
Não substitui conferência visual em navegador.
"""
import hashlib
import html
import http.cookiejar
import os
from pathlib import Path
import re
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def web_port():
    value = os.environ.get("WEB_PORT")
    env = ROOT / ".env"
    if value is None and env.exists():
        for line in env.read_text(encoding="utf-8-sig").splitlines():
            key, sep, configured = line.partition("=")
            if sep and key.strip() == "WEB_PORT":
                value = configured.strip().strip("\"'")
    port = int(value or "8080")
    assert 1 <= port <= 65535, "WEB_PORT inválida"
    return port


BASE = f"http://127.0.0.1:{web_port()}"


def credentials():
    values = dict(os.environ)
    env = ROOT / ".env"
    if env.exists():
        for line in env.read_text(encoding="utf-8-sig").splitlines():
            key, sep, value = line.partition("=")
            if sep and key.strip() in ("IDENTITY_ADMIN_EMAIL", "IDENTITY_ADMIN_PASSWORD"):
                values.setdefault(key.strip(), value.strip().strip("\"'"))
    assert values.get("IDENTITY_ADMIN_EMAIL") and values.get("IDENTITY_ADMIN_PASSWORD"), "Credenciais locais ausentes"
    return values["IDENTITY_ADMIN_EMAIL"], values["IDENTITY_ADMIN_PASSWORD"]


def request(client, path, data=None, headers=None):
    req = urllib.request.Request(BASE + path, data=data, headers=headers or {})
    try:
        response = client.open(req, timeout=30)
    except urllib.error.HTTPError as response:
        return response.code, response.geturl(), response.headers, response.read()
    with response:
        return response.status, response.geturl(), response.headers, response.read()


def search(client, page, term=None, year=None):
    params = {}
    if term is not None:
        params["Termo"] = term
    if year is not None:
        params["AnoReferencia"] = year
    path = "/pesquisa" if page == 1 else f"/pesquisa/{page}"
    if params:
        path += "?" + urllib.parse.urlencode(params)
    status, url, headers, body = request(client, path)
    text = body.decode("utf-8")
    assert status == 200 and "/login" not in url, "Pesquisa autenticada indisponível"
    assert "alert-danger" not in text, "Pesquisa retornou mensagem de erro"
    assert f"Página {page} de" in text, "Página de resultados incorreta"
    return text


def pdf_links(text):
    return re.findall(r'href="(/documentos/[a-f0-9-]+/pdf)"', text)


def antiforgery_token(body):
    text = body.decode("utf-8") if isinstance(body, bytes) else body
    match = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', text)
    assert match, "Token antiforgery ausente no formulário"
    return html.unescape(match.group(1))


def check_pdf(client, text):
    link = pdf_links(text)[0]
    status, url, headers, body = request(client, link)
    assert status == 200 and headers.get_content_type() == "application/pdf", "PDF indisponível"
    assert body.startswith(b"%PDF-"), "Conteúdo não é PDF"
    assert "no-store" in headers.get("Cache-Control", ""), "PDF sem bloqueio de cache"
    first_article = re.search(r'<article\b.*?</article>', text, re.S).group()
    filename = html.unescape(re.search(r'<h2[^>]*>(.*?)</h2>', first_article, re.S).group(1))
    files = list((ROOT / "data" / "input").rglob(filename))
    assert len(files) == 1, "Arquivo fictício ausente ou ambíguo"
    assert hashlib.sha256(body).digest() == hashlib.sha256(files[0].read_bytes()).digest(), "PDF não corresponde ao resultado"
    status, _, range_headers, partial = request(client, link, headers={"Range": "bytes=0-4"})
    assert status == 206 and partial == b"%PDF-", "Range de PDF inválido"
    return link


def main():
    client = urllib.request.build_opener(urllib.request.ProxyHandler({}),
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    status, url, _, _ = request(client, "/")
    assert status == 200 and "/login" in url, "Início sem login incorreto"
    status, url, _, _ = request(client, "/pesquisa")
    assert status == 200 and "/login" in url, "Pesquisa anônima não bloqueada"
    print("OK: início e pesquisa anônima exigem login")
    email, password = credentials()
    status, _, _, _ = request(client, "/auth/login", urllib.parse.urlencode({"email": email, "password": password}).encode())
    assert status == 400, "Login sem token antiforgery não foi rejeitado"
    _, _, _, login_body = request(client, "/login")
    status, _, _, _ = request(client, "/auth/login", urllib.parse.urlencode({"email": email, "password": password, "__RequestVerificationToken": "invalido"}).encode())
    assert status == 400, "Login com token inválido não foi rejeitado"
    status, url, _, body = request(client, "/auth/login", urllib.parse.urlencode({"email": email, "password": password, "__RequestVerificationToken": antiforgery_token(login_body)}).encode())
    assert status == 200 and "/pesquisa" in url and "alert-danger" not in body.decode(), "Login falhou"
    status, url, _, _ = request(client, "/")
    assert status == 200 and "/pesquisa" in url, "Início autenticado não direciona à pesquisa"
    print("OK: login e acesso à pesquisa")
    first = search(client, 1, "JASMIM AZUL", 2026)
    second = search(client, 2, "JASMIM AZUL", 2026)
    assert len(pdf_links(first)) == 20 and pdf_links(second), "Massa insuficiente para paginação"
    for text in (first, second):
        text = html.unescape(text)
        controls = list(re.finditer(r'<nav[^>]*aria-label="Paginação dos resultados".*?</nav>', text, re.S))
        assert len(controls) == 2, "Paginação deve aparecer no topo e no final"
        cards = list(re.finditer(r'<article class="card mb-3".*?</article>', text, re.S))
        assert controls[0].end() <= cards[0].start() and controls[1].start() >= cards[-1].end(), "Posição da paginação incorreta"
        assert controls[0].group() == controls[1].group(), "Controles de paginação divergentes"
        assert "bi-house-door-fill-nav-menu" not in text and not re.search(r'>\s*Início\s*<', text), "Menu mantém item Início redundante"
    assert not set(pdf_links(first)) & set(pdf_links(second)), "Paginação repetiu documentos"
    assert pdf_links(search(client, 1, "JASMIM AZUL", 2026)) == pdf_links(first), "Retorno alterou resultados"
    hrefs = [html.unescape(value) for value in re.findall(r'href="([^"]+)"', second)]
    assert any("/pesquisa/1?" in value and "AnoReferencia=2026" in value and "JASMIM" in value for value in hrefs), "Link perdeu filtros"
    print("OK: próxima/anterior com filtros e resultados distintos")
    changed = search(client, 1, "GIRASSOL DOURADO", 2025)
    assert pdf_links(changed) and not set(pdf_links(changed)) & set(pdf_links(first)), "Nova pesquisa manteve resultados antigos"
    assert 'action="/pesquisa"' in second and 'method="get"' in second, "Formulário não reinicia rota"
    assert not pdf_links(search(client, 1, "GIRASSOL DOURADO", 2026)), "Filtro de ano ignorado"
    cleared = search(client, 1)
    assert pdf_links(cleared) and "Termo=" not in cleared and "AnoReferencia=" not in cleared, "Limpeza manteve filtros"
    assert pdf_links(search(client, 1, "", "")) == pdf_links(cleared), "Submit de campos vazios não limpou filtros"
    assert not pdf_links(search(client, 1, "TERMO_FICTICIO_INEXISTENTE_93817")), "Resultados vazios incorretos"
    print("OK: nova pesquisa, troca de ano, limpeza e resultados vazios")
    links = [check_pdf(client, text) for text in (first, second, changed)]
    print("OK: PDF correto nas duas páginas e no ano 2025; MIME, hash, cache e range")
    status, _, _, _ = request(client, "/auth/logout", b"")
    assert status == 400, "Logout sem token antiforgery não foi rejeitado"
    status, _, _, _ = request(client, "/auth/logout", urllib.parse.urlencode({"__RequestVerificationToken": "invalido"}).encode())
    assert status == 400, "Logout com token inválido não foi rejeitado"
    authenticated = search(client, 1)
    print("OK: login/logout rejeitam token ausente ou inválido; sessão preservada")
    status, url, _, _ = request(client, "/auth/logout", urllib.parse.urlencode({"__RequestVerificationToken": antiforgery_token(authenticated)}).encode())
    assert status == 200 and "/login" in url, "Logout falhou"
    for link in links:
        status, url, headers, body = request(client, link)
        assert "/login" in url or status in (401, 403), "PDF acessível após logout"
        assert not body.startswith(b"%PDF-"), "PDF vazou após logout"
    print("OK: logout e bloqueio das URLs de PDF")


if __name__ == "__main__":
    try:
        main()
    except AssertionError as error:
        print("FALHA:", str(error))
        raise SystemExit(1)
    except Exception as error:
        print("FALHA de execução:", type(error).__name__)
        raise SystemExit(1)
