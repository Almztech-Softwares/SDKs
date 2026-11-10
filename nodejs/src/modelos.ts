/*
  Tipos do wire format da API, em camelCase como o JSON real (ver DESIGN.md seção 0). Os nomes
  são os mesmos do JSON da API, então o objeto vai e volta sem mapeamento. Propriedades undefined/null não são enviadas: a API aplica o
  padrão dela, e null num campo numérico seria recusado com 400.
*/

export type StatusProtocolo =
	| "EM_FILA"
	| "EM_PROCESSAMENTO"
	| "PENDENTE_PREFEITURA"
	| "PROCESSADO_COM_SUCESSO"
	| "REJEITADO"
	| "CANCELADO"
	| "ERRO_INTERNO";

export type StatusNota = "EM_PROCESSAMENTO" | "EMITIDA" | "CANCELADA" | "DENEGADA" | "REJEITADA" | "PENDENTE_PREFEITURA";

/** Data pura no formato `yyyy-MM-dd` (o `DateOnly` da API). */
export type DataIso = string;

// ---------- Emissão (POST /NFSe/Emitir) ----------

export interface RecepcaoLote {
	/** CPF/CNPJ do emitente cadastrado. */
	identificacaoEmitente: string;
	notas: NotaFiscalResumida[];
	/** `false` gera protocolo de homologação (`HML-`), `true` de produção (`PRD-`). É por lote, não do client. */
	enviarParaProducao: boolean;
	/** Webhook só deste lote, além dos cadastrados para o emitente. */
	urlWebhook?: string;
}

export interface NotaFiscalResumida {
	rps: IdentificacaoDaNota;
	destinatario: Destinatario;
	servicos: Servico[];
	condicoesPagamentos?: CondicaoPagamento[];
	observacao?: string;
	/** Nome com o typo do schema real da API (`Subtituicao`) — não corrigir, quebraria o wire format. */
	subtituicaoRps?: SubtituicaoRps;
}

export interface IdentificacaoDaNota {
	/** Opcional quando o emitente tem controle automático de RPS: a API numera. */
	numeroRPS?: string;
	numeroNota?: string;
	serie?: string;
	/** Tipo do RPS (ex.: `"1"` = RPS). */
	tipo?: string;
	/** 1 = normal (padrão da API), 2 = cancelado. */
	status?: number;
	dataEmissao?: DataIso;
	dataCompetencia?: DataIso;
	codigoVerificacao?: string;
	chaveNota?: string;
}

export interface Destinatario {
	cpfCnpj: string;
	razaoSocial: string;
	nomeFantasia?: string;
	inscricaoEstadual?: string;
	inscricaoMunicipal?: string;
	telefone?: string;
	/** Precisa do `codigoIBGEMunicipio`: sem ele a nota falha depois, no processamento assíncrono. */
	endereco?: Endereco;
}

export interface Endereco {
	logradouro?: string;
	numero?: string;
	complemento?: string;
	bairro?: string;
	cep?: string;
	/** Código BACEN do país; a API assume 1058 (Brasil). */
	codigoPais?: number;
	codigoIBGEUF?: number;
	codigoIBGEMunicipio?: number;
	siglaUF?: string;
	descricaoUF?: string;
	descricaoMunicipio?: string;
}

/** Os códigos fiscais são números, como na API; os omitidos usam o padrão dela. */
export interface Servico {
	codigo: string;
	descricao: string;
	codigoTributacao?: string;
	/** Código IBGE (7 dígitos) do município onde o serviço foi prestado. */
	localPrestacao?: string;
	responsavelPelaRetencao?: number;
	/** ABRASF: 1 Exigível, 2 Não incidência, 3 Isenção, 4 Exportação, 5 Imunidade, 6/7 Suspensa. */
	exigibilidadeISS?: number;
	/** Varia conforme o município de prestação. */
	tipoTributacao?: number;
	/** Código IBGE do município onde o ISS incide. */
	municipioIncidencia?: number;
	aliquotaISS?: number;
	/** 1 = retido pelo tomador, 2 = não retido. */
	retemISS?: number;
	quantidade?: number;
	valorUnitario: number;
	valorDesconto?: number;
	valorAcrescimo?: number;
	codigoCnae?: string;
	numeroProcesso?: string;
	nbs?: string;
	ibsCbs?: IbsCbs;
	pisCofins?: PisCofins;
	totaisDosTributos?: TotaisDosTributos;
}

export interface IbsCbs {
	codigoOperacao?: string;
	operacaoTrabalhista?: string;
	categoriaOperacao?: string;
	categoriaEntePublico?: string;
	descricaoCategoriaEntePublico?: string;
	regime?: { cst?: string; cct?: string };
}

export interface PisCofins {
	modalidadeRetencao?: string;
	baseDeCalculo?: number;
	cst?: string;
	pis?: { percentual?: number; valor?: number };
	cofins?: { percentual?: number; valor?: number };
}

export interface TotaisDosTributos {
	percentualDosTributosFederais?: number;
	percentualDosTributosEstaduais?: number;
	percentualDosTributosMunicipais?: number;
	percentualDosTributosDoSimplesNacional?: number;
}

export interface CondicaoPagamento {
	condicao?: string;
	pagamentos?: { parcela: number; dataPagamento?: DataIso; valor: number }[];
}

export interface SubtituicaoRps {
	numeroRPS?: string;
	numeroNota?: string;
	serie?: string;
	tipo?: string;
	/** Chave de acesso da nota substituída (Emissor Nacional). */
	chaveNota?: string;
	codigoMotivo?: string;
	motivo?: string;
}

// ---------- Protocolo (emissão, cancelamento, polling e webhook) ----------

export interface ProtocoloResponse {
	protocolo: string;
	status: StatusProtocolo;
	mensagem?: string;
	notas?: ProtocoloNotaResponse[];
	dadosCancelamento?: { codigoNota: number; numeroNota?: string };
}

export interface ProtocoloNotaResponse {
	codigoNota: number;
	tipoNota?: string;
	numero?: string;
	rps?: string;
	serie?: string;
	dataEmissao: DataIso;
	situacao: StatusNota;
	codigoVerificacao?: string;
	chaveAcesso?: string;
	/** URL de download do XML. */
	xml?: string;
	/** URL de download do PDF. */
	pdf?: string;
	dataHoraCancelamento?: string;
	protocolo: string;
}

// ---------- Listagens ----------

export interface NotaFiscal {
	codigoNota: number;
	numeroNota?: string;
	codigoEmitente: number;
	cpfCnpjEmitente?: string;
	nomeEmitente?: string;
	numeroRPS?: string;
	serieRPS?: string;
	loteRPS: number;
	valorTotal: number;
	status: StatusNota;
	dataAutorizacao?: DataIso;
	tempoDeProcessamento?: string;
	protocolo: string;
	chaveAcesso?: string;
}

export interface ResultadoConsultaComFiltros {
	notas: NotaFiscal[];
	paginaAtual: number;
	totalDeRegistros: number;
	totalEmProcessamento: number;
	totalEmitidas: number;
	totalCanceladas: number;
	totalDenegadas: number;
	totalRejeitadas: number;
	totalPendentes: number;
	/** Por status, ignorando o próprio filtro de status (chave `all` = todos). */
	contagensPorAba: Record<string, number>;
}

// ---------- Planilha ----------

export interface ResultadoImportacaoPlanilha {
	/** Um protocolo por lote gerado; acompanhe cada um como o de `emitir`. */
	protocolos: ProtocoloResponse[];
	totalLinhasLidas: number;
	totalNotasValidas: number;
	erros: { linha: number; mensagem: string }[];
}

// ---------- Emitente ----------

/**
 * `atualizar` grava todos os campos do corpo, inclusive os padrões fiscais: atualize a partir do emitente
 * consultado, nunca de um objeto novo. Senhas, certificado, controle automático e sequências de RPS mantêm o
 * valor gravado quando vêm vazios. `senhaCertificado`/`senhaPrefeitura` vêm em claro nas leituras — nunca logar.
 */
export interface Emitente {
	codigo?: number;
	razaoSocial: string;
	nomeFantasia?: string;
	cpfCnpj: string;
	inscricaoEstadual?: string;
	inscricaoMunicipal?: string;
	telefone?: string;
	email?: string;
	chaveCertificado?: string;
	senhaCertificado?: string;
	vencimentoCertificado?: string;
	usuarioPrefeitura?: string;
	senhaPrefeitura?: string;
	codigoCnae?: string;
	incentivadorCultural?: boolean;
	incentivoFiscal?: string;
	optanteSimples?: boolean;
	regimeTributacao?: number;
	controleAutomaticoRPS?: boolean;
	sequenciaRPSProducao?: number;
	sequenciaRPSHomologacao?: number;
	sequenciaRPSProducaoAlterada?: boolean;
	sequenciaRPSHomologacaoAlterada?: boolean;
	ativo?: boolean;
	naturezaOperacaoPadrao?: number;
	calculaRetencoesFederaisPadrao?: boolean;
	tipoRpsPadrao?: string;
	serieRpsPadrao?: string;
	exigibilidadeIssPadrao?: number;
	tipoTributacaoPadrao?: number;
	issRetidoPadrao?: number;
	aliquotaIssPadrao?: number;
	codigoServicoPadrao?: string;
	endereco?: EnderecoEmitente;
}

export interface EnderecoEmitente {
	codigo?: number;
	codigoEmitente?: number;
	logradouro?: string;
	bairro?: string;
	numero?: string;
	cep?: string;
	complemento?: string;
	/** Define a prefeitura (e a integradora) usada na emissão. */
	codigoIBGEMunicipio: number;
	codigoIBGEUF?: number;
}

export interface ResultadoGravacaoEmitente {
	mensagem: string;
	cpfCnpj?: string;
	codigo?: number;
}

export interface ResultadoUploadCertificado {
	mensagem: string;
	chaveCertificado?: string;
	vencimento?: string;
	aviso?: string;
}

// ---------- Webhook e municípios ----------

/** Serializado como número na API. */
export enum WebhookEvento {
	Emissao = 0,
	Cancelamento = 1,
	Rejeicao = 2,
}

/** Um registro = um evento; para vários eventos, cadastre vários registros. */
export interface Webhook {
	codigo?: string;
	evento: WebhookEvento;
	ativo?: boolean;
	url: string;
	codigoEmitente: number;
}

export interface MunicipioHomologado {
	/** Código IBGE (7 dígitos). */
	id: number;
	/** `"{códigoIBGE} - {nome}"`. */
	text: string;
}

export interface Mensagem {
	mensagem: string;
}
