import {
	AlmztechErroApi,
	AlmztechErroAutenticacao,
	AlmztechErroAutorizacao,
	AlmztechErroLimiteDeRequisicoes,
	AlmztechErroRede,
	AlmztechErroServidor,
	AlmztechErroValidacao,
} from "./erros.js";
import { mascararApiKey, mascararCorpoJson } from "./log.js";

export const VERSAO_SDK = "0.2.0-beta.1";

/** Só operações seguras (leituras) são retentadas, e só em falha de rede ou 5xx. Ver DESIGN.md seção 4. */
export interface OpcoesDeRetry {
	tentativas?: number;
	intervaloInicialMs?: number;
	multiplicador?: number;
	intervaloMaximoMs?: number;
}

export interface OpcoesDoClient {
	/** Padrão: produção. O ambiente fiscal é por chamada (`enviarParaProducao`), não por URL. */
	baseUrl?: string;
	/** Timeout de rede por chamada (padrão 30 s). Não confundir com o tempo de processamento da prefeitura. */
	timeoutMs?: number;
	retry?: OpcoesDeRetry;
	/** `fetch` próprio (proxy corporativo, instrumentação). Padrão: o `fetch` global do Node 18+. */
	fetch?: typeof fetch;
	/** Recebe só mensagens já mascaradas. */
	logger?: (mensagem: string) => void;
}

interface Requisicao {
	metodo: "GET" | "POST" | "PUT" | "DELETE";
	caminho: string;
	corpo?: unknown;
	permiteRetry: boolean;
}

export class Transporte {
	private readonly baseUrl: string;
	private readonly timeoutMs: number;
	private readonly retry: Required<OpcoesDeRetry>;
	private readonly fetch: typeof fetch;
	private readonly logger?: (mensagem: string) => void;

	constructor(
		private readonly apiKey: string,
		opcoes: OpcoesDoClient = {},
	) {
		if (!apiKey?.trim()) throw new Error("apiKey é obrigatório.");

		this.baseUrl = (opcoes.baseUrl ?? "https://api.almztech.com.br").replace(/\/+$/, "");
		this.timeoutMs = opcoes.timeoutMs ?? 30_000;
		this.retry = { tentativas: 3, intervaloInicialMs: 500, multiplicador: 2, intervaloMaximoMs: 10_000, ...opcoes.retry };
		this.fetch = opcoes.fetch ?? globalThis.fetch;
		this.logger = opcoes.logger;
		this.logger?.(`AlmztechClient configurado (apiKey=${mascararApiKey(apiKey)}, baseUrl=${this.baseUrl})`);
	}

	async json<T>(requisicao: Requisicao): Promise<T> {
		const resposta = await this.enviar(requisicao);
		const texto = await resposta.text();
		this.logger?.(`<- ${resposta.status} ${requisicao.metodo} ${requisicao.caminho} ${mascararCorpoJson(texto)}`);

		if (!resposta.ok) throw mapearErro(resposta, texto);
		return (texto ? JSON.parse(texto) : undefined) as T;
	}

	async bytes(caminho: string): Promise<Uint8Array> {
		const resposta = await this.enviar({ metodo: "GET", caminho, permiteRetry: true });
		if (!resposta.ok) throw mapearErro(resposta, await resposta.text());
		return new Uint8Array(await resposta.arrayBuffer());
	}

	private async enviar({ metodo, caminho, corpo, permiteRetry }: Requisicao): Promise<Response> {
		const tentativas = permiteRetry ? Math.max(1, this.retry.tentativas) : 1;
		const json = corpo === undefined ? undefined : JSON.stringify(corpo, omitirNulos);

		for (let tentativa = 1; ; tentativa++) {
			if (json) this.logger?.(`-> ${metodo} ${caminho} ${mascararCorpoJson(json)}`);

			let resposta: Response;
			try {
				resposta = await this.fetch(`${this.baseUrl}/${caminho}`, {
					method: metodo,
					headers: {
						"x-api-key": this.apiKey,
						"User-Agent": `almztech-nodejs-sdk/${VERSAO_SDK}`,
						...(json ? { "Content-Type": "application/json" } : {}),
					},
					body: json,
					signal: AbortSignal.timeout(this.timeoutMs),
				});
			} catch (erro) {
				if (tentativa >= tentativas)
					throw new AlmztechErroRede(`Falha de rede chamando ${metodo} ${caminho}: ${(erro as Error).message}`, { cause: erro });

				await esperar(this.atraso(tentativa));
				continue;
			}

			if (resposta.status >= 500 && tentativa < tentativas) {
				await resposta.body?.cancel();
				await esperar(this.atraso(tentativa));
				continue;
			}

			return resposta;
		}
	}

	private atraso(tentativa: number): number {
		const base = Math.min(this.retry.intervaloInicialMs * this.retry.multiplicador ** (tentativa - 1), this.retry.intervaloMaximoMs);
		return base + Math.random() * base * 0.2;
	}
}

/** Campo numérico da API (tipo não anulável) recusa null com 400: omitir deixa valer o padrão dela. */
function omitirNulos(_chave: string, valor: unknown): unknown {
	return valor === null ? undefined : valor;
}

const esperar = (ms: number) => new Promise((resolver) => setTimeout(resolver, ms));

function mapearErro(resposta: Response, corpo: string): AlmztechErroApi {
	const mensagem = extrairMensagem(corpo);
	switch (resposta.status) {
		case 400:
		case 422:
			return new AlmztechErroValidacao(resposta.status, mensagem, corpo);
		case 401:
			return new AlmztechErroAutenticacao(401, mensagem, corpo);
		case 403:
			return new AlmztechErroAutorizacao(403, mensagem, corpo);
		case 429:
			return new AlmztechErroLimiteDeRequisicoes(mensagem, lerRetryAfter(resposta.headers.get("retry-after")), corpo);
		default:
			return resposta.status >= 500
				? new AlmztechErroServidor(resposta.status, mensagem, corpo)
				: new AlmztechErroApi(resposta.status, mensagem, corpo);
	}
}

function lerRetryAfter(valor: string | null): number | undefined {
	if (!valor) return undefined;
	const segundos = Number(valor);
	if (Number.isFinite(segundos)) return segundos * 1000;
	const data = Date.parse(valor);
	return Number.isNaN(data) ? undefined : Math.max(0, data - Date.now());
}

/** `{ mensagem }` é o formato de erro da API; o 400 do ASP.NET vem como ProblemDetails, com os erros por campo em `errors`. */
function extrairMensagem(corpo: string): string {
	if (!corpo.trim()) return "(sem corpo de resposta)";

	try {
		const json = JSON.parse(corpo);
		if (typeof json?.mensagem === "string") return json.mensagem;
		if (json?.errors && typeof json.errors === "object")
			return Object.entries(json.errors as Record<string, unknown>)
				.flatMap(([campo, erros]) => (Array.isArray(erros) ? erros : [erros]).map((erro) => `${campo}: ${erro}`))
				.join("; ");
	} catch {
		// corpo não é JSON
	}
	return corpo;
}
