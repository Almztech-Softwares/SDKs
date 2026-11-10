import type { ProtocoloResponse } from "./modelos.js";

/** Base de todas as exceções do SDK. Ver DESIGN.md seção 3. */
export class AlmztechErro extends Error {
	constructor(mensagem: string, opcoes?: { cause?: unknown }) {
		super(mensagem, opcoes);
		this.name = new.target.name;
	}
}

/** Erro devolvido pela API (`{ "mensagem": "..." }`). Não há código de erro estruturado: `mensagem` é texto livre. */
export class AlmztechErroApi extends AlmztechErro {
	constructor(
		readonly statusCode: number,
		readonly mensagem: string,
		readonly corpoBruto?: string,
	) {
		super(`A API da Almztech retornou ${statusCode}: ${mensagem}`);
	}
}

/** 422 (regra de negócio, inclusive franquia do plano esgotada) ou 400 (corpo recusado antes do controller, com os erros por campo). */
export class AlmztechErroValidacao extends AlmztechErroApi {}

/** 401 — token ausente, inválido ou revogado, ou licença inválida para produção. Só a mensagem diferencia. */
export class AlmztechErroAutenticacao extends AlmztechErroApi {}

/** 403 — papel sem permissão para o endpoint. */
export class AlmztechErroAutorizacao extends AlmztechErroApi {}

/** 429 — limite de requisições. Não é retentado sozinho: `retryAfterMs` diz quanto esperar. */
export class AlmztechErroLimiteDeRequisicoes extends AlmztechErroApi {
	constructor(
		mensagem: string,
		readonly retryAfterMs: number | undefined,
		corpoBruto?: string,
	) {
		super(429, mensagem, corpoBruto);
	}
}

/** 5xx — erro interno; em produção a mensagem pode vir genérica. */
export class AlmztechErroServidor extends AlmztechErroApi {}

/** Falha de rede, timeout ou DNS antes de qualquer resposta HTTP. */
export class AlmztechErroRede extends AlmztechErro {}

/** O polling estourou o tempo total; `ultimoProtocolo` permite continuar manualmente. */
export class AlmztechErroTempoDePollingEsgotado extends AlmztechErro {
	constructor(
		readonly protocolo: string,
		readonly ultimoProtocolo: ProtocoloResponse | undefined,
	) {
		super(`O protocolo ${protocolo} não concluiu no tempo de espera (último status: ${ultimoProtocolo?.status ?? "desconhecido"}).`);
	}
}

/** O protocolo terminou em `REJEITADO` ou `ERRO_INTERNO`; o motivo está em `protocolo.mensagem`. */
export class AlmztechErroProtocoloRejeitado extends AlmztechErro {
	constructor(readonly protocolo: ProtocoloResponse) {
		super(`O protocolo ${protocolo.protocolo} terminou em ${protocolo.status}: ${protocolo.mensagem ?? "(sem mensagem)"}`);
	}
}

/** Payload de webhook não reconhecido. */
export class AlmztechErroWebhook extends AlmztechErro {}
