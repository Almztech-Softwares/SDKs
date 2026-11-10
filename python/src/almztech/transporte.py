"""HTTP do SDK: header ``x-api-key``, retry só em leituras, mapeamento de erros e JSON sem nulos."""

from __future__ import annotations

import json
import random
import socket
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from email.utils import parsedate_to_datetime
from typing import Any, Callable, Mapping, Optional, Tuple

from .erros import (
    AlmztechErroApi,
    AlmztechErroAutenticacao,
    AlmztechErroAutorizacao,
    AlmztechErroLimiteDeRequisicoes,
    AlmztechErroRede,
    AlmztechErroServidor,
    AlmztechErroValidacao,
)
from .log import mascarar_api_key, mascarar_corpo_json

VERSAO_SDK = "0.2.0-beta.1"

# (status, headers, corpo) — o que um envio HTTP devolve.
RespostaHttp = Tuple[int, Mapping[str, str], bytes]

# Envio HTTP injetável (proxy corporativo, instrumentação, testes): (método, url, headers, corpo, timeout).
# Deve devolver a resposta inclusive em 4xx/5xx e lançar OSError só em falha de rede.
EnvioHttp = Callable[[str, str, Mapping[str, str], Optional[bytes], float], RespostaHttp]


@dataclass
class OpcoesDeRetry:
    """Só operações seguras (leituras) são retentadas, e só em falha de rede ou 5xx (DESIGN.md seção 4)."""

    tentativas: int = 3
    intervalo_inicial: float = 0.5
    multiplicador: float = 2.0
    intervalo_maximo: float = 10.0

    def atraso(self, tentativa: int) -> float:
        base = min(self.intervalo_inicial * self.multiplicador ** (tentativa - 1), self.intervalo_maximo)
        return base + random.random() * base * 0.2


@dataclass
class OpcoesDoClient:
    #: Padrão: produção. O ambiente fiscal é por chamada (``enviarParaProducao``), não por URL.
    base_url: str = "https://api.almztech.com.br"
    #: Timeout de rede por chamada, em segundos. Não é o tempo de processamento da prefeitura.
    timeout: float = 30.0
    retry: OpcoesDeRetry = field(default_factory=OpcoesDeRetry)
    envio_http: Optional[EnvioHttp] = None
    #: Recebe só mensagens já mascaradas.
    logger: Optional[Callable[[str], None]] = None


def _envio_urllib(metodo: str, url: str, headers: Mapping[str, str], corpo: Optional[bytes], timeout: float) -> RespostaHttp:
    requisicao = urllib.request.Request(url, data=corpo, headers=dict(headers), method=metodo)
    try:
        with urllib.request.urlopen(requisicao, timeout=timeout) as resposta:
            return resposta.status, dict(resposta.headers), resposta.read()
    except urllib.error.HTTPError as erro:
        return erro.code, dict(erro.headers or {}), erro.read()


def _sem_nulos(valor: Any) -> Any:
    """Campo numérico da API (tipo não anulável) recusa null com 400: omitir deixa valer o padrão dela."""
    if isinstance(valor, dict):
        return {chave: _sem_nulos(item) for chave, item in valor.items() if item is not None}
    if isinstance(valor, (list, tuple)):
        return [_sem_nulos(item) for item in valor]
    return valor


class Transporte:
    def __init__(self, api_key: str, opcoes: Optional[OpcoesDoClient] = None):
        if not api_key or not api_key.strip():
            raise ValueError("api_key é obrigatório.")
        self._api_key = api_key
        self._opcoes = opcoes or OpcoesDoClient()
        self._envio = self._opcoes.envio_http or _envio_urllib
        self._base_url = self._opcoes.base_url.rstrip("/")
        self._log(f"AlmztechClient configurado (apiKey={mascarar_api_key(api_key)}, baseUrl={self._base_url})")

    def json(self, metodo: str, caminho: str, corpo: Any = None, permite_retry: bool = False) -> Any:
        status, headers, conteudo = self._enviar(metodo, caminho, corpo, permite_retry)
        texto = conteudo.decode("utf-8") if conteudo else ""
        self._log(f"<- {status} {metodo} {caminho} {mascarar_corpo_json(texto)}")
        if status >= 400:
            raise _mapear_erro(status, headers, texto)
        return json.loads(texto) if texto.strip() else None

    def bytes(self, caminho: str) -> bytes:
        status, headers, conteudo = self._enviar("GET", caminho, None, True)
        if status >= 400:
            raise _mapear_erro(status, headers, conteudo.decode("utf-8", errors="replace"))
        return conteudo

    def _enviar(self, metodo: str, caminho: str, corpo: Any, permite_retry: bool) -> RespostaHttp:
        retry = self._opcoes.retry
        tentativas = max(1, retry.tentativas) if permite_retry else 1
        dados = None if corpo is None else json.dumps(_sem_nulos(corpo), ensure_ascii=False).encode("utf-8")
        headers = {
            "x-api-key": self._api_key,
            "User-Agent": f"almztech-python-sdk/{VERSAO_SDK}",
            "Accept": "application/json",
        }
        if dados is not None:
            headers["Content-Type"] = "application/json"

        tentativa = 1
        while True:
            if dados is not None:
                self._log(f"-> {metodo} {caminho} {mascarar_corpo_json(dados.decode('utf-8'))}")
            try:
                resposta = self._envio(metodo, f"{self._base_url}/{caminho}", headers, dados, self._opcoes.timeout)
            except (OSError, socket.timeout) as erro:
                if tentativa >= tentativas:
                    raise AlmztechErroRede(f"Falha de rede chamando {metodo} {caminho}: {erro}") from erro
                time.sleep(retry.atraso(tentativa))
                tentativa += 1
                continue

            if resposta[0] >= 500 and tentativa < tentativas:
                time.sleep(retry.atraso(tentativa))
                tentativa += 1
                continue
            return resposta

    def _log(self, mensagem: str) -> None:
        if self._opcoes.logger:
            self._opcoes.logger(mensagem)


def _mapear_erro(status: int, headers: Mapping[str, str], corpo: str) -> AlmztechErroApi:
    mensagem = _extrair_mensagem(corpo)
    if status in (400, 422):
        return AlmztechErroValidacao(status, mensagem, corpo)
    if status == 401:
        return AlmztechErroAutenticacao(status, mensagem, corpo)
    if status == 403:
        return AlmztechErroAutorizacao(status, mensagem, corpo)
    if status == 429:
        retry_after = next((valor for chave, valor in headers.items() if chave.lower() == "retry-after"), None)
        return AlmztechErroLimiteDeRequisicoes(mensagem, _ler_retry_after(retry_after), corpo)
    if status >= 500:
        return AlmztechErroServidor(status, mensagem, corpo)
    return AlmztechErroApi(status, mensagem, corpo)


def _ler_retry_after(valor: Optional[str]) -> Optional[float]:
    if not valor:
        return None
    try:
        return float(valor)
    except ValueError:
        pass
    try:
        return max(0.0, parsedate_to_datetime(valor).timestamp() - time.time())
    except (TypeError, ValueError):
        return None


def _extrair_mensagem(corpo: str) -> str:
    """``{mensagem}`` é o formato de erro da API; o 400 do ASP.NET vem como ProblemDetails, com ``errors`` por campo."""
    if not corpo.strip():
        return "(sem corpo de resposta)"
    try:
        dados = json.loads(corpo)
    except ValueError:
        return corpo
    if isinstance(dados, dict):
        if isinstance(dados.get("mensagem"), str):
            return dados["mensagem"]
        if isinstance(dados.get("errors"), dict):
            return "; ".join(
                f"{campo}: {erro}"
                for campo, erros in dados["errors"].items()
                for erro in (erros if isinstance(erros, list) else [erros])
            )
    return corpo
