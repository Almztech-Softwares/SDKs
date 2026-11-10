import type { StatusNota } from "./modelos.js";

/** Operadores aceitos pela API, enviados pelo nome. */
export type OperadorFiltro =
	| "Igual"
	| "Diferente"
	| "MaiorQue"
	| "MaiorOuIgual"
	| "MenorQue"
	| "MenorOuIgual"
	| "Entre"
	| "Contem"
	| "IniciaCom"
	| "TerminaCom";

/** Campos aceitos pela API. Campo desconhecido ou valor vazio é ignorado pela API. */
export const CamposFiltro = {
	protocolo: "protocolo",
	codigoNota: "codigoNota",
	nomeEmitente: "nomeEmitente",
	cpfCnpjEmitente: "cpfCnpjEmitente",
	numeroRps: "numeroRps",
	serieRps: "serieRps",
	loteRps: "loteRps",
	valorTotal: "valorTotal",
	/** Data `yyyy-MM-dd`. */
	dataEmissao: "dataEmissao",
	/** Vários status separados por `|`; só com `Igual`. */
	status: "status",
} as const;

/** Prefixo do protocolo das notas: o ambiente das listagens. */
export const Ambiente = { producao: "PRD", homologacao: "HML" } as const;

/** Valor sempre como texto no wire format: datas `yyyy-MM-dd`, decimais com ponto. */
export interface Filtro {
	campo: string;
	operador: OperadorFiltro;
	valor: string;
	/** Fim do intervalo, só com `Entre`. */
	valorFinal?: string;
}

export interface ParametrosFiltros {
	/** Obrigatório: `Ambiente.producao` ou `Ambiente.homologacao`. */
	ambiente: string;
	/** Busca livre por código, CPF/CNPJ do emitente, número da nota/RPS, nome fantasia ou protocolo. */
	filtroRapido?: string;
	filtros?: Filtro[];
	pagina?: number;
	/** Máximo real de 100 por página (clamp no servidor). */
	limiteDaPagina?: number;
}

const data = (valor: Date | string) => (typeof valor === "string" ? valor : valor.toISOString().slice(0, 10));

/** Construtores que formatam o valor como a API espera. */
export const Filtros = {
	texto: (campo: string, operador: OperadorFiltro, valor: string): Filtro => ({ campo, operador, valor }),
	numero: (campo: string, operador: OperadorFiltro, valor: number): Filtro => ({ campo, operador, valor: String(valor) }),
	entreValores: (campo: string, inicio: number, fim: number): Filtro => ({ campo, operador: "Entre", valor: String(inicio), valorFinal: String(fim) }),
	/** `Date` vira a data em UTC; passe `yyyy-MM-dd` para controlar o fuso. */
	data: (operador: OperadorFiltro, valor: Date | string): Filtro => ({ campo: CamposFiltro.dataEmissao, operador, valor: data(valor) }),
	periodo: (inicio: Date | string, fim: Date | string): Filtro => ({ campo: CamposFiltro.dataEmissao, operador: "Entre", valor: data(inicio), valorFinal: data(fim) }),
	comStatus: (...status: StatusNota[]): Filtro => ({ campo: CamposFiltro.status, operador: "Igual", valor: status.join("|") }),
};
