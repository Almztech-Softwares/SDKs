"""SDK oficial Python da Almztech (DESIGN.md).

Exemplo::

    from almztech import AlmztechClient

    client = AlmztechClient(os.environ["ALMZTECH_API_KEY"])
    protocolo = client.notas_fiscais.emitir(lote)
    resultado = client.notas_fiscais.aguardar_conclusao(protocolo["protocolo"])
"""

from __future__ import annotations

from typing import Optional

from . import filtros
from .erros import (
    AlmztechErro,
    AlmztechErroApi,
    AlmztechErroAutenticacao,
    AlmztechErroAutorizacao,
    AlmztechErroLimiteDeRequisicoes,
    AlmztechErroProtocoloRejeitado,
    AlmztechErroRede,
    AlmztechErroServidor,
    AlmztechErroTempoDePollingEsgotado,
    AlmztechErroValidacao,
    AlmztechErroWebhook,
)
from .log import mascarar_api_key, mascarar_corpo_json
from .recursos import Emitentes, Municipios, NotasFiscais, Webhooks
from .transporte import VERSAO_SDK, OpcoesDeRetry, OpcoesDoClient, Transporte
from .webhook import EventoWebhook, ler_webhook

__version__ = VERSAO_SDK


class AlmztechClient:
    """Client raiz. Crie um por aplicação e reaproveite.

    A api key está em "Primeiros passos" no Portal; nunca guarde login e senha no seu sistema.
    """

    def __init__(self, api_key: str, opcoes: Optional[OpcoesDoClient] = None):
        transporte = Transporte(api_key, opcoes)
        self.notas_fiscais = NotasFiscais(transporte)
        self.emitentes = Emitentes(transporte)
        self.webhooks = Webhooks(transporte)
        self.municipios = Municipios(transporte)


__all__ = [
    "AlmztechClient",
    "OpcoesDoClient",
    "OpcoesDeRetry",
    "filtros",
    "ler_webhook",
    "EventoWebhook",
    "mascarar_api_key",
    "mascarar_corpo_json",
    "AlmztechErro",
    "AlmztechErroApi",
    "AlmztechErroValidacao",
    "AlmztechErroAutenticacao",
    "AlmztechErroAutorizacao",
    "AlmztechErroLimiteDeRequisicoes",
    "AlmztechErroServidor",
    "AlmztechErroRede",
    "AlmztechErroTempoDePollingEsgotado",
    "AlmztechErroProtocoloRejeitado",
    "AlmztechErroWebhook",
]
