"""Tipos do wire format (``TypedDict``): os dados são ``dict`` em camelCase, exatamente como o JSON da API.

Servem para o editor e o mypy; em runtime são dicionários comuns. Chaves com valor ``None`` não são enviadas
(a API aplica o padrão dela, e null num campo numérico seria recusado com 400).
"""

from __future__ import annotations

from typing import List, Optional

try:
    from typing import Literal, TypedDict
except ImportError:  # pragma: no cover
    from typing_extensions import Literal, TypedDict  # type: ignore

StatusProtocolo = Literal[
    "EM_FILA", "EM_PROCESSAMENTO", "PENDENTE_PREFEITURA", "PROCESSADO_COM_SUCESSO", "REJEITADO", "CANCELADO", "ERRO_INTERNO"
]
StatusNota = Literal["EM_PROCESSAMENTO", "EMITIDA", "CANCELADA", "DENEGADA", "REJEITADA", "PENDENTE_PREFEITURA"]


class Endereco(TypedDict, total=False):
    logradouro: str
    numero: str
    complemento: str
    bairro: str
    cep: str
    codigoPais: int
    codigoIBGEUF: int
    #: Obrigatório para o tomador: sem ele a nota falha depois, no processamento assíncrono.
    codigoIBGEMunicipio: int
    siglaUF: str


class Destinatario(TypedDict, total=False):
    cpfCnpj: str
    razaoSocial: str
    nomeFantasia: str
    inscricaoEstadual: str
    inscricaoMunicipal: str
    telefone: str
    endereco: Endereco


class Servico(TypedDict, total=False):
    codigo: str
    descricao: str
    codigoTributacao: str
    localPrestacao: str
    responsavelPelaRetencao: int
    #: ABRASF: 1 Exigível, 2 Não incidência, 3 Isenção, 4 Exportação, 5 Imunidade, 6/7 Suspensa.
    exigibilidadeISS: int
    tipoTributacao: int
    municipioIncidencia: int
    aliquotaISS: float
    #: 1 = retido pelo tomador, 2 = não retido.
    retemISS: int
    quantidade: float
    valorUnitario: float
    valorDesconto: float
    valorAcrescimo: float
    codigoCnae: str
    numeroProcesso: str
    nbs: str
    ibsCbs: dict
    pisCofins: dict
    totaisDosTributos: dict


class IdentificacaoDaNota(TypedDict, total=False):
    #: Opcional com o controle automático de RPS do emitente: a API numera.
    numeroRPS: str
    serie: str
    tipo: str
    status: int
    #: ``yyyy-MM-dd``.
    dataEmissao: str
    dataCompetencia: str


class NotaFiscalResumida(TypedDict, total=False):
    rps: IdentificacaoDaNota
    destinatario: Destinatario
    servicos: List[Servico]
    #: ``[{"condicao": ..., "pagamentos": [{"parcela": 1, "dataPagamento": "yyyy-MM-dd", "valor": ...}]}]``.
    condicoesPagamentos: List[dict]
    observacao: str
    #: Nome com o typo do schema real (``Subtituicao``): numeroRPS, numeroNota, serie, tipo, chaveNota, codigoMotivo, motivo.
    subtituicaoRps: dict


class RecepcaoLote(TypedDict, total=False):
    identificacaoEmitente: str
    notas: List[NotaFiscalResumida]
    #: ``False`` = homologação (``HML-``), ``True`` = produção (``PRD-``). É por lote.
    enviarParaProducao: bool
    urlWebhook: str


class ProtocoloNotaResponse(TypedDict, total=False):
    codigoNota: int
    tipoNota: str
    numero: str
    rps: str
    serie: str
    dataEmissao: str
    situacao: StatusNota
    codigoVerificacao: str
    chaveAcesso: str
    xml: str
    pdf: str
    dataHoraCancelamento: str
    protocolo: str


class ProtocoloResponse(TypedDict, total=False):
    protocolo: str
    status: StatusProtocolo
    mensagem: Optional[str]
    notas: List[ProtocoloNotaResponse]
    dadosCancelamento: dict
