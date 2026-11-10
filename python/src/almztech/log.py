"""Mascaramento central de log (DESIGN.md seção 6).

Cobre request e response: ``senhaCertificado`` e ``senhaPrefeitura`` vêm em claro inclusive nas leituras de
emitente. Todo log automático do SDK passa por aqui.
"""

from __future__ import annotations

import json
from typing import Any, Optional

_CAMPOS_SENSIVEIS = {"apikey", "senha", "senhacertificado", "senhaprefeitura", "base64", "arquivobase64", "login"}
_MASCARA = "***"


def mascarar_api_key(api_key: str) -> str:
    """Mostra só os últimos 4 caracteres da api key."""
    if not api_key:
        return _MASCARA
    return _MASCARA + (api_key[-4:] if len(api_key) > 4 else api_key)


def mascarar_corpo_json(corpo: Optional[str]) -> str:
    """Cópia do JSON com todo campo sensível mascarado. Corpo não-JSON nunca é devolvido cru."""
    if not corpo or not corpo.strip():
        return ""
    try:
        return json.dumps(_mascarar(json.loads(corpo)), ensure_ascii=False)
    except ValueError:
        return "<corpo não-JSON omitido da sanitização de log>"


def _mascarar(valor: Any) -> Any:
    if isinstance(valor, list):
        return [_mascarar(item) for item in valor]
    if isinstance(valor, dict):
        return {
            chave: _MASCARA if chave.lower() in _CAMPOS_SENSIVEIS else _mascarar(item)
            for chave, item in valor.items()
        }
    return valor
