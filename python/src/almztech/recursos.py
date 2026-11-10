"""Sub-clients por recurso (DESIGN.md seção 1)."""

from __future__ import annotations

import base64
import time
from pathlib import Path
from typing import Any, BinaryIO, Callable, Iterator, List, Optional, Union
from urllib.parse import quote, urlencode

from .erros import AlmztechErroProtocoloRejeitado, AlmztechErroTempoDePollingEsgotado
from .modelos import ProtocoloNotaResponse, ProtocoloResponse, RecepcaoLote
from .transporte import Transporte

#: Caminho de arquivo, bytes ou um arquivo aberto em modo binário.
Arquivo = Union[str, Path, bytes, bytearray, BinaryIO]


def _base64(arquivo: Arquivo) -> str:
    if isinstance(arquivo, (str, Path)):
        conteudo = Path(arquivo).read_bytes()
    elif isinstance(arquivo, (bytes, bytearray)):
        conteudo = bytes(arquivo)
    else:
        conteudo = arquivo.read()
    return base64.b64encode(conteudo).decode("ascii")


def _seg(valor: Any) -> str:
    return quote(str(valor), safe="")


class NotasFiscais:
    """Emissão, consulta, reconsulta, cancelamento, planilha e downloads de NFS-e (rota ``NFSe/*``)."""

    def __init__(self, transporte: Transporte):
        self._t = transporte

    def emitir(self, lote: RecepcaoLote) -> ProtocoloResponse:
        """``POST /NFSe/Emitir``. Devolve só o protocolo em fila; acompanhe com ``aguardar_conclusao`` ou o webhook.

        Produção exige licença válida e franquia disponível; homologação não consome franquia. **Nunca retentado
        automaticamente**: a API não tem chave de idempotência e um reenvio pode duplicar a nota.
        """
        return self._t.json("POST", "NFSe/Emitir", lote)

    def aguardar_conclusao(
        self,
        protocolo: str,
        intervalo_inicial: float = 2.0,
        multiplicador: float = 1.5,
        intervalo_maximo: float = 30.0,
        timeout_total: float = 300.0,
        a_cada_tentativa: Optional[Callable[[ProtocoloResponse], None]] = None,
    ) -> ProtocoloResponse:
        """Consulta o protocolo com intervalos crescentes até um status terminal.

        ``REJEITADO``/``ERRO_INTERNO`` lançam ``AlmztechErroProtocoloRejeitado``; estourar ``timeout_total``
        (segundos) lança ``AlmztechErroTempoDePollingEsgotado`` com o último protocolo.
        """
        inicio = time.monotonic()
        atraso = intervalo_inicial
        while True:
            ultimo = self.consultar_protocolo(protocolo)
            if a_cada_tentativa:
                a_cada_tentativa(ultimo)

            status = ultimo.get("status")
            if status in ("PROCESSADO_COM_SUCESSO", "CANCELADO"):
                return ultimo
            if status in ("REJEITADO", "ERRO_INTERNO"):
                raise AlmztechErroProtocoloRejeitado(dict(ultimo))
            if time.monotonic() - inicio >= timeout_total:
                raise AlmztechErroTempoDePollingEsgotado(protocolo, dict(ultimo))

            time.sleep(atraso)
            atraso = min(atraso * multiplicador, intervalo_maximo)

    def consultar_protocolo(self, protocolo: str) -> ProtocoloResponse:
        return self._ler(f"NFSe/Consultar/Protocolo/{_seg(protocolo)}")

    def consultar_por_codigo(self, codigo_nota: int) -> ProtocoloNotaResponse:
        return self._ler(f"NFSe/Consultar/{codigo_nota}")

    def consultar_por_numero(self, numero_nota: str) -> ProtocoloNotaResponse:
        """Pelo número da NFS-e na prefeitura; havendo mais de uma entre os seus emitentes, vem a mais recente."""
        return self._ler(f"NFSe/Consultar/Numero/{_seg(numero_nota)}")

    def consultar_lista(self, ambiente: str) -> List[dict]:
        """As 50 notas mais recentes do ambiente (``PRODUCAO``/``HOMOLOGACAO`` de ``almztech.filtros``)."""
        return self._ler(f"NFSe/ConsultarLista/{_seg(ambiente)}")

    def consultar_lista_por_termo(self, ambiente: str, termo: str) -> List[dict]:
        """Busca por CNPJ, código, número, razão social, nome fantasia ou protocolo (até 50)."""
        return self._ler(f"NFSe/ConsultarListaPorTermo/{_seg(ambiente)}/{_seg(termo)}")

    def consultar_com_filtros(
        self,
        ambiente: str,
        filtros: Optional[List[dict]] = None,
        filtro_rapido: Optional[str] = None,
        pagina: int = 1,
        limite_da_pagina: int = 50,
    ) -> dict:
        """POST por causa do corpo, mas é leitura (retentado como os GET). Limite real de 100 por página.

        Devolve ``notas``, ``paginaAtual``, ``totalDeRegistros``, os totais por status e ``contagensPorAba``.
        """
        corpo = {
            "ambiente": ambiente,
            "filtroRapido": filtro_rapido,
            "filtros": filtros,
            "pagina": pagina,
            "limiteDaPagina": limite_da_pagina,
        }
        return self._t.json("POST", "NFSe/ConsultarComFiltros", corpo, permite_retry=True)

    def paginas(
        self, ambiente: str, filtros: Optional[List[dict]] = None, filtro_rapido: Optional[str] = None, limite_da_pagina: int = 50
    ) -> Iterator[dict]:
        """Percorre todas as páginas de ``consultar_com_filtros``, nota a nota."""
        pagina = 1
        while True:
            resultado = self.consultar_com_filtros(ambiente, filtros, filtro_rapido, pagina, limite_da_pagina)
            notas = resultado.get("notas") or []
            yield from notas
            total_de_paginas = -(-resultado.get("totalDeRegistros", 0) // max(1, limite_da_pagina))
            if not notas or pagina >= total_de_paginas:
                return
            pagina += 1

    def reconsultar(self, codigo_nota: int) -> ProtocoloNotaResponse:
        """Consulta a nota de novo na prefeitura e atualiza a situação local. Repetir é seguro."""
        return self._ler(f"NFSe/Reconsultar/{codigo_nota}")

    def reconsultar_por_chave_acesso(self, chave_acesso: str, enviar_para_producao: bool) -> ProtocoloNotaResponse:
        """Reconsulta pela chave de acesso do Emissor Nacional; atualiza a nota local de mesmo número."""
        return self._t.json(
            "POST", f"NFSe/Reconsultar/ChaveAcesso/{_seg(chave_acesso)}", {"enviarParaProducao": enviar_para_producao}, permite_retry=True
        )

    def cancelar(self, codigo_nota: int, url_webhook: Optional[str] = None) -> ProtocoloResponse:
        """``POST /NFSe/Cancelar/{codigo}``. Assíncrono como a emissão; não retentado."""
        return self._t.json("POST", f"NFSe/Cancelar/{codigo_nota}", {"urlWebhook": url_webhook})

    def baixar_xml(self, codigo_nota: int) -> bytes:
        """Bytes do XML; gravar em disco fica com você."""
        return self._t.bytes(f"NFSe/XML/{codigo_nota}")

    def baixar_pdf(self, codigo_nota: int) -> bytes:
        return self._t.bytes(f"NFSe/PDF/{codigo_nota}")

    def baixar_modelo_planilha(self) -> bytes:
        """O ``.xlsx`` modelo de ``importar_planilha``."""
        return self._t.bytes("NFSe/ModeloPlanilha")

    def importar_planilha(
        self, identificacao_emitente: str, planilha: Arquivo, enviar_para_producao: bool, url_webhook: Optional[str] = None
    ) -> dict:
        """Emite as notas de uma planilha no formato do modelo, com os padrões fiscais do emitente.

        O emitente precisa do controle automático de RPS. Devolve ``protocolos`` (um por lote) e ``erros`` por linha.
        Não retentado (emite notas).
        """
        corpo = {
            "arquivoBase64": _base64(planilha),
            "identificacaoEmitente": identificacao_emitente,
            "enviarParaProducao": enviar_para_producao,
            "urlWebhook": url_webhook,
        }
        return self._t.json("POST", "NFSe/ImportarPlanilha", corpo)

    def _ler(self, caminho: str) -> Any:
        return self._t.json("GET", caminho, permite_retry=True)


class Emitentes:
    """CRUD de emitente e upload do certificado A1 (rota ``Emitente/*``).

    ``atualizar`` grava todos os campos, inclusive os padrões fiscais: parta do emitente consultado. As leituras
    trazem ``senhaCertificado``/``senhaPrefeitura`` em claro — nunca logue esses dicionários.
    """

    def __init__(self, transporte: Transporte):
        self._t = transporte

    def consultar(self) -> List[dict]:
        return self._t.json("GET", "Emitente/Consultar", permite_retry=True)

    def consultar_por_codigo(self, codigo: int) -> dict:
        return self._t.json("GET", f"Emitente/Consultar/{codigo}", permite_retry=True)

    def consultar_por_cpf_cnpj(self, cpf_cnpj: str) -> dict:
        return self._t.json("GET", f"Emitente/ConsultarCpfCnpj/{_seg(cpf_cnpj)}", permite_retry=True)

    def consultar_por_termo(self, termo: str) -> List[dict]:
        return self._t.json("GET", f"Emitente/ConsultaPorTermo/{_seg(termo)}", permite_retry=True)

    def cadastrar(self, emitente: dict) -> dict:
        """Devolve ``{"mensagem", "cpfCnpj", "codigo"}``."""
        return self._t.json("POST", "Emitente/Cadastrar", emitente)

    def atualizar(self, emitente: dict) -> dict:
        return self._t.json("PUT", "Emitente/Atualizar", emitente)

    def excluir(self, codigo: int) -> dict:
        return self._t.json("DELETE", f"Emitente/Excluir/{codigo}")

    def upload_certificado(self, cnpj_emissor: str, certificado_pfx: Arquivo, senha: str) -> dict:
        """Envia o ``.pfx``, convertido para Base64 aqui. Devolve ``chaveCertificado``, ``vencimento`` e ``aviso``."""
        corpo = {"cnpjEmissor": cnpj_emissor, "base64": _base64(certificado_pfx), "senha": senha}
        return self._t.json("POST", "Emitente/UploadCertificado", corpo)


class Webhooks:
    """CRUD e teste de webhook (fisicamente em ``Emitente/Webhook*``). Evento: 0 Emissão, 1 Cancelamento, 2 Rejeição."""

    EMISSAO = 0
    CANCELAMENTO = 1
    REJEICAO = 2

    def __init__(self, transporte: Transporte):
        self._t = transporte

    def obter(self, codigo: str) -> dict:
        return self._t.json("GET", f"Emitente/Webhook/{_seg(codigo)}", permite_retry=True)

    def listar_por_emitente(self, codigo_emitente: int) -> List[dict]:
        return self._t.json("GET", f"Emitente/Webhooks/{codigo_emitente}", permite_retry=True)

    def criar(self, codigo_emitente: int, evento: int, url: str, ativo: bool = True) -> dict:
        """Não retentado: a API não barra URL duplicada, e um retry cego criaria dois registros."""
        corpo = {"codigoEmitente": codigo_emitente, "evento": evento, "url": url, "ativo": ativo}
        return self._t.json("POST", "Emitente/Webhook", corpo)

    def atualizar(self, webhook: dict) -> dict:
        return self._t.json("PUT", "Emitente/Webhook", webhook)

    def excluir(self, codigo: str) -> dict:
        return self._t.json("DELETE", f"Emitente/Webhook/{_seg(codigo)}")

    def testar(self, url: str, evento: int) -> dict:
        """Dispara um payload de exemplo do evento para a URL, sem precisar de webhook cadastrado."""
        return self._t.json("POST", "Emitente/Webhook/Teste", {"url": url, "evento": evento})


class Municipios:
    """Municípios já homologados (``CoberturaNacional/MunicipiosHomologados``)."""

    def __init__(self, transporte: Transporte):
        self._t = transporte

    def homologados(self, codigo_ibge: Optional[int] = None, termo_de_busca: Optional[str] = None) -> List[dict]:
        """Pelo código IBGE ou por um termo; sem nenhum dos dois vem vazio. Até 20 ``{"id": <IBGE>, "text"}``."""
        parametros = {}
        if codigo_ibge is not None:
            parametros["codigo"] = codigo_ibge
        if termo_de_busca:
            parametros["termoDeBusca"] = termo_de_busca
        sufixo = f"?{urlencode(parametros)}" if parametros else ""
        return self._t.json("GET", f"CoberturaNacional/MunicipiosHomologados{sufixo}", permite_retry=True)
