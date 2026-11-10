import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { AlmztechClient } from "../src/index.js";

/**
 * Lê um fixture compartilhado de fixtures/, sem o campo de proveniência `_comentario`. Os de erro guardam o
 * corpo em `body` e os de listagem em `response`; devolve só o payload, como a API o enviaria.
 */
export function fixture(caminho: string): string {
	// build-test/test/ → fixtures/
	const raiz = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "fixtures");
	const json = JSON.parse(readFileSync(join(raiz, caminho), "utf8"));
	delete json._comentario;
	return JSON.stringify(json.body ?? json.response ?? json);
}

interface RespostaFalsa {
	status: number;
	corpo?: string;
	headers?: Record<string, string>;
	falhaDeRede?: boolean;
}

/** `fetch` falso: responde em fila e guarda caminho, método e corpo de cada chamada. */
export class FetchFalso {
	private readonly fila: RespostaFalsa[] = [];
	readonly chamadas: { metodo: string; caminho: string; corpo?: string; headers: Record<string, string> }[] = [];

	responder(status: number, corpo = "", headers: Record<string, string> = {}): this {
		this.fila.push({ status, corpo, headers });
		return this;
	}

	falharRede(): this {
		this.fila.push({ status: 0, falhaDeRede: true });
		return this;
	}

	readonly fetch: typeof fetch = async (url, init) => {
		const endereco = new URL(String(url));
		this.chamadas.push({
			metodo: init?.method ?? "GET",
			caminho: endereco.pathname + endereco.search,
			corpo: init?.body as string | undefined,
			headers: init?.headers as Record<string, string>,
		});

		const resposta = this.fila.shift();
		if (!resposta) throw new Error("FetchFalso sem respostas suficientes na fila.");
		if (resposta.falhaDeRede) throw new TypeError("fetch failed");
		return new Response(resposta.corpo || null, { status: resposta.status, headers: { "Content-Type": "application/json", ...resposta.headers } });
	};

	corpoJson(indice = 0): any {
		return JSON.parse(this.chamadas[indice].corpo!);
	}
}

export function criarClient(falso: FetchFalso): AlmztechClient {
	return new AlmztechClient("api-key-de-teste", {
		baseUrl: "https://sdk-tests.local",
		fetch: falso.fetch,
		retry: { tentativas: 3, intervaloInicialMs: 1, intervaloMaximoMs: 5 },
	});
}
