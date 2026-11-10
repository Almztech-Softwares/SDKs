import { Emitentes, Municipios, NotasFiscais, Webhooks } from "./recursos.js";
import { Transporte, type OpcoesDoClient } from "./transporte.js";

/**
 * Client raiz do SDK Node da Almztech (DESIGN.md). Crie uma instância por aplicação e reaproveite.
 * A api key está em "Primeiros passos" no Portal; nunca guarde login e senha no seu sistema.
 *
 * @example
 * const client = new AlmztechClient(process.env.ALMZTECH_API_KEY!);
 * const protocolo = await client.notasFiscais.emitir(lote);
 * const resultado = await client.notasFiscais.aguardarConclusao(protocolo.protocolo);
 */
export class AlmztechClient {
	readonly notasFiscais: NotasFiscais;
	readonly emitentes: Emitentes;
	readonly webhooks: Webhooks;
	readonly municipios: Municipios;

	constructor(apiKey: string, opcoes?: OpcoesDoClient) {
		const transporte = new Transporte(apiKey, opcoes);
		this.notasFiscais = new NotasFiscais(transporte);
		this.emitentes = new Emitentes(transporte);
		this.webhooks = new Webhooks(transporte);
		this.municipios = new Municipios(transporte);
	}
}

export * from "./erros.js";
export * from "./filtros.js";
export * from "./modelos.js";
export { mascararApiKey, mascararCorpoJson } from "./log.js";
export type { Arquivo, Emitentes, Municipios, NotasFiscais, OpcoesDeEspera, Webhooks } from "./recursos.js";
export { VERSAO_SDK, type OpcoesDeRetry, type OpcoesDoClient } from "./transporte.js";
export { lerWebhook, type EventoWebhook, type TipoEventoWebhook } from "./webhook.js";
