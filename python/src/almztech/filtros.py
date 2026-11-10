"""Filtros de ``consultar_com_filtros`` (DESIGN.md seção 7).

O valor vai sempre como texto: datas ``yyyy-MM-dd`` e decimais com ponto. Campo desconhecido ou valor vazio é
ignorado pela API (não filtra), por isso prefira as constantes e os construtores daqui.
"""

from __future__ import annotations

import datetime as dt
from decimal import Decimal
from typing import Optional, Union

PRODUCAO = "PRD"
HOMOLOGACAO = "HML"


class CamposFiltro:
    PROTOCOLO = "protocolo"
    CODIGO_NOTA = "codigoNota"
    NOME_EMITENTE = "nomeEmitente"
    CPF_CNPJ_EMITENTE = "cpfCnpjEmitente"
    NUMERO_RPS = "numeroRps"
    SERIE_RPS = "serieRps"
    LOTE_RPS = "loteRps"
    VALOR_TOTAL = "valorTotal"
    #: Data ``yyyy-MM-dd``.
    DATA_EMISSAO = "dataEmissao"
    #: Vários status separados por ``|``; só com ``Igual``.
    STATUS = "status"


class Operador:
    IGUAL = "Igual"
    DIFERENTE = "Diferente"
    MAIOR_QUE = "MaiorQue"
    MAIOR_OU_IGUAL = "MaiorOuIgual"
    MENOR_QUE = "MenorQue"
    MENOR_OU_IGUAL = "MenorOuIgual"
    ENTRE = "Entre"
    CONTEM = "Contem"
    INICIA_COM = "IniciaCom"
    TERMINA_COM = "TerminaCom"


Numero = Union[int, float, Decimal]
Data = Union[dt.date, str]


def _texto_numero(valor: Numero) -> str:
    return format(Decimal(str(valor)), "f") if not isinstance(valor, int) else str(valor)


def _texto_data(valor: Data) -> str:
    return valor if isinstance(valor, str) else valor.isoformat()


def filtro(campo: str, operador: str, valor: str, valor_final: Optional[str] = None) -> dict:
    item = {"campo": campo, "operador": operador, "valor": valor}
    if valor_final is not None:
        item["valorFinal"] = valor_final
    return item


def numero(campo: str, operador: str, valor: Numero) -> dict:
    return filtro(campo, operador, _texto_numero(valor))


def entre_valores(campo: str, inicio: Numero, fim: Numero) -> dict:
    return filtro(campo, Operador.ENTRE, _texto_numero(inicio), _texto_numero(fim))


def data(operador: str, valor: Data) -> dict:
    return filtro(CamposFiltro.DATA_EMISSAO, operador, _texto_data(valor))


def periodo(inicio: Data, fim: Data) -> dict:
    return filtro(CamposFiltro.DATA_EMISSAO, Operador.ENTRE, _texto_data(inicio), _texto_data(fim))


def com_status(*status: str) -> dict:
    return filtro(CamposFiltro.STATUS, Operador.IGUAL, "|".join(status))
