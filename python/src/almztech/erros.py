"""Hierarquia de exceções do SDK (DESIGN.md seção 3)."""

from __future__ import annotations

from typing import Any, Optional


class AlmztechErro(Exception):
    """Base de todas as exceções do SDK."""


class AlmztechErroApi(AlmztechErro):
    """Erro devolvido pela API (``{"mensagem": "..."}``). Não há código estruturado: ``mensagem`` é texto livre."""

    def __init__(self, status_code: int, mensagem: str, corpo_bruto: Optional[str] = None):
        super().__init__(f"A API da Almztech retornou {status_code}: {mensagem}")
        self.status_code = status_code
        self.mensagem = mensagem
        self.corpo_bruto = corpo_bruto


class AlmztechErroValidacao(AlmztechErroApi):
    """422 (regra de negócio, inclusive franquia do plano esgotada) ou 400 (corpo recusado, com os erros por campo)."""


class AlmztechErroAutenticacao(AlmztechErroApi):
    """401 — token ausente, inválido ou revogado, ou licença inválida para produção. Só a mensagem diferencia."""


class AlmztechErroAutorizacao(AlmztechErroApi):
    """403 — papel sem permissão para o endpoint."""


class AlmztechErroLimiteDeRequisicoes(AlmztechErroApi):
    """429 — limite de requisições. Não é retentado sozinho: ``retry_after`` (segundos) diz quanto esperar."""

    def __init__(self, mensagem: str, retry_after: Optional[float], corpo_bruto: Optional[str] = None):
        super().__init__(429, mensagem, corpo_bruto)
        self.retry_after = retry_after


class AlmztechErroServidor(AlmztechErroApi):
    """5xx — erro interno; em produção a mensagem pode vir genérica."""


class AlmztechErroRede(AlmztechErro):
    """Falha de rede, timeout ou DNS antes de qualquer resposta HTTP."""


class AlmztechErroTempoDePollingEsgotado(AlmztechErro):
    """O polling estourou o tempo total; ``ultimo_protocolo`` permite continuar manualmente."""

    def __init__(self, protocolo: str, ultimo_protocolo: Optional[dict[str, Any]]):
        status = (ultimo_protocolo or {}).get("status", "desconhecido")
        super().__init__(f"O protocolo {protocolo} não concluiu no tempo de espera (último status: {status}).")
        self.protocolo = protocolo
        self.ultimo_protocolo = ultimo_protocolo


class AlmztechErroProtocoloRejeitado(AlmztechErro):
    """O protocolo terminou em ``REJEITADO`` ou ``ERRO_INTERNO``; o motivo está em ``protocolo["mensagem"]``."""

    def __init__(self, protocolo: dict[str, Any]):
        super().__init__(
            f"O protocolo {protocolo.get('protocolo')} terminou em {protocolo.get('status')}: "
            f"{protocolo.get('mensagem') or '(sem mensagem)'}"
        )
        self.protocolo = protocolo


class AlmztechErroWebhook(AlmztechErro):
    """Payload de webhook não reconhecido."""
