"""Leitura do payload recebido no seu endpoint de webhook (DESIGN.md seção 5)."""

from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Union

from .erros import AlmztechErroWebhook
from .modelos import ProtocoloResponse

EMISSAO = "Emissao"
CANCELAMENTO = "Cancelamento"
REJEICAO = "Rejeicao"


@dataclass
class EventoWebhook:
    #: ``EMISSAO``, ``CANCELAMENTO`` ou ``REJEICAO``.
    tipo: str
    #: Payload completo (mesmo schema do polling).
    protocolo: ProtocoloResponse


def ler_webhook(corpo: Union[str, bytes, dict]) -> EventoWebhook:
    """Classifica o corpo: ``notas`` não vazio → Emissão; senão ``dadosCancelamento`` → Cancelamento; senão status
    ``REJEITADO`` → Rejeição (motivo em ``mensagem``); senão lança ``AlmztechErroWebhook``.

    Não verifica autenticidade: a API não assina o webhook. Proteger o endpoint (token secreto na URL, allowlist
    de IP, mTLS) é responsabilidade de quem o recebe.
    """
    if isinstance(corpo, (str, bytes)):
        if not corpo.strip():
            raise AlmztechErroWebhook("Corpo do webhook vazio.")
        try:
            protocolo = json.loads(corpo)
        except ValueError as erro:
            raise AlmztechErroWebhook("Corpo do webhook não é um JSON válido.") from erro
    else:
        protocolo = corpo

    if not isinstance(protocolo, dict):
        raise AlmztechErroWebhook("Corpo do webhook não é um objeto.")
    if protocolo.get("notas"):
        return EventoWebhook(EMISSAO, protocolo)  # type: ignore[arg-type]
    if protocolo.get("dadosCancelamento"):
        return EventoWebhook(CANCELAMENTO, protocolo)  # type: ignore[arg-type]
    if protocolo.get("status") == "REJEITADO":
        return EventoWebhook(REJEICAO, protocolo)  # type: ignore[arg-type]
    raise AlmztechErroWebhook(
        f"Payload de webhook não reconhecido (protocolo='{protocolo.get('protocolo')}', status='{protocolo.get('status')}'): "
        "sem 'notas', sem 'dadosCancelamento' e status diferente de REJEITADO."
    )
