"""Carga moderada de leitura no ambiente local fictício; não simula acervo real."""
from concurrent.futures import ThreadPoolExecutor
import http.cookiejar
import json
import math
import time
import urllib.parse
import urllib.request

from validar_homologacao_http import ROOT, antiforgery_token, credentials, request


def main():
    client = urllib.request.build_opener(urllib.request.ProxyHandler({}),
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    _, _, _, body = request(client, "/login")
    email, password = credentials()
    status, url, _, _ = request(client, "/auth/login", urllib.parse.urlencode(dict(
        email=email, password=password, __RequestVerificationToken=antiforgery_token(body))).encode())
    assert status == 200 and "/pesquisa" in url, "Login para carga falhou"
    paths = ["/pesquisa?Termo=JASMIM+AZUL&AnoReferencia=2026",
             "/pesquisa/2?Termo=JASMIM+AZUL&AnoReferencia=2026",
             "/pesquisa?Termo=GIRASSOL+DOURADO&AnoReferencia=2025",
             "/pesquisa?Termo=TERMO_FICTICIO_INEXISTENTE_93817"]

    def measure(index):
        start = time.perf_counter()
        status, url, _, body = request(client, paths[index % len(paths)])
        assert status == 200 and "/login" not in url and b"alert-danger" not in body, "Consulta de carga falhou"
        return (time.perf_counter() - start) * 1000

    start = time.perf_counter()
    try:
        with ThreadPoolExecutor(max_workers=4) as pool:
            timings = sorted(pool.map(measure, range(40)))
        elapsed = time.perf_counter() - start
        result = dict(requests=len(timings), concurrency=4, failures=0,
                      elapsed_seconds=round(elapsed, 3), requests_per_second=round(len(timings) / elapsed, 2),
                      p50_ms=round(timings[math.ceil(len(timings) * .5) - 1], 2),
                      p95_ms=round(timings[math.ceil(len(timings) * .95) - 1], 2), max_ms=round(timings[-1], 2),
                      scope="40 leituras HTTP Web/API em massa fictícia local; não valida 100 mil PDFs")
        output = ROOT / "artifacts" / "validation"
        output.mkdir(parents=True, exist_ok=True)
        (output / "carga-http.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
        print(json.dumps(result, ensure_ascii=True))
    finally:
        _, _, _, body = request(client, "/pesquisa")
        request(client, "/auth/logout", urllib.parse.urlencode(dict(
            __RequestVerificationToken=antiforgery_token(body))).encode())


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FALHA de carga:", type(error).__name__)
        raise SystemExit(1)
