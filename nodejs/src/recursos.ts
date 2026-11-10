import { readFile } from "node:fs/promises";
import { AlmztechErroProtocoloRejeitado, AlmztechErroTempoDePollingEsgotado } from "./erros.js";
import type { ParametrosFiltros } from "./filtros.js";
import type {
	Emitente,
	Mensagem,
	MunicipioHomologado,
	NotaFiscal,
	ProtocoloNotaResponse,
	ProtocoloResponse,
	RecepcaoLote,
	ResultadoConsultaComFiltros,
	ResultadoGravacaoEmitente,
	ResultadoImportacaoPlanilha,
	ResultadoUploadCertificado,
	Webhook,
	WebhookEvento,
} from "./modelos.js";
import type { Transporte } from "./transporte.js";

/** Caminho de arquivo, bytes ou um stream/iterável de bytes. */
export type Arquivo = string | Uint8Array | AsyncIterable<Uint8Array>;

async function lerArquivo(arquivo: Arquivo): Promise<string> {
	if (typeof arquivo === "string") return (await readFile(arquivo)).toString("base64");
	if (arquivo instanceof Uint8Array) return Buffer.from(arquivo).toString("base64");

	const partes: Uint8Array[] = [];
	for await (const parte of arquivo) partes.push(parte);
	return Buffer.concat(partes).toString("base64");
}

const seg = encodeURIComponent;

export interface OpcoesDeEspera {
	intervaloInicialMs?: number;
	multiplicador?: number;
	intervaloMaximoMs?: number;
	/** Padrão: 5 minutos. */
	timeoutTotalMs?: number;
	aCadaTentativa?: (protocolo: ProtocoloResponse) => void;
}

/** Emissão, consulta, reconsulta, cancelamento, planilha e downloads de NFS-e (rota `NFSe/*`). */
export class NotasFiscais {
	constructor(private readonly transporte: Transporte) {}

	/**
	 * `POST /NFSe/Emitir`. Devolve só o protocolo em fila: acompanhe com `aguardarConclusao` ou pelo webhook.
	 * Produção exige licença válida e franquia disponível; homologação não consome franquia. **Nunca retentado
	 * automaticamente** — a API não tem chave de idempotência e um reenvio pode duplicar a nota.
	 */
	emitir(lote: RecepcaoLote): Promise<ProtocoloResponse> {
		return this.transporte.json({ metodo: "POST", caminho: "NFSe/Emitir", corpo: lote, permiteRetry: false });
	}

	/**
	 * Consulta o protocolo com intervalos crescentes até um status terminal. `REJEITADO`/`ERRO_INTERNO` lançam
	 * `AlmztechErroProtocoloRejeitado`; estourar o tempo lança `AlmztechErroTempoDePollingEsgotado`.
	 */
	async aguardarConclusao(protocolo: string, opcoes: OpcoesDeEspera = {}): Promise<ProtocoloResponse> {
		const { intervaloInicialMs = 2_000, multiplicador = 1.5, intervaloMaximoMs = 30_000, timeoutTotalMs = 300_000, aCadaTentativa } = opcoes;
		const inicio = Date.now();
		let atraso = intervaloInicialMs;
		let ultimo: ProtocoloResponse | undefined;

		for (;;) {
			ultimo = await this.consultarProtocolo(protocolo);
			aCadaTentativa?.(ultimo);

			if (ultimo.status === "PROCESSADO_COM_SUCESSO" || ultimo.status === "CANCELADO") return ultimo;
			if (ultimo.status === "REJEITADO" || ultimo.status === "ERRO_INTERNO") throw new AlmztechErroProtocoloRejeitado(ultimo);
			if (Date.now() - inicio >= timeoutTotalMs) throw new AlmztechErroTempoDePollingEsgotado(protocolo, ultimo);

			await new Promise((resolver) => setTimeout(resolver, atraso));
			atraso = Math.min(atraso * multiplicador, intervaloMaximoMs);
		}
	}

	consultarProtocolo(protocolo: string): Promise<ProtocoloResponse> {
		return this.ler(`NFSe/Consultar/Protocolo/${seg(protocolo)}`);
	}

	consultarPorCodigo(codigoNota: number): Promise<ProtocoloNotaResponse> {
		return this.ler(`NFSe/Consultar/${codigoNota}`);
	}

	/** Pelo número da NFS-e na prefeitura; havendo mais de uma entre os seus emitentes, vem a mais recente. */
	consultarPorNumero(numeroNota: string): Promise<ProtocoloNotaResponse> {
		return this.ler(`NFSe/Consultar/Numero/${seg(numeroNota)}`);
	}

	/** As 50 notas mais recentes do ambiente (`Ambiente.producao`/`homologacao`). */
	consultarLista(ambiente: string): Promise<NotaFiscal[]> {
		return this.ler(`NFSe/ConsultarLista/${seg(ambiente)}`);
	}

	/** Busca por CNPJ, código, número, razão social, nome fantasia ou protocolo (até 50). */
	consultarListaPorTermo(ambiente: string, termo: string): Promise<NotaFiscal[]> {
		return this.ler(`NFSe/ConsultarListaPorTermo/${seg(ambiente)}/${seg(termo)}`);
	}

	/** POST por causa do corpo, mas é leitura: retentado como os GET. */
	consultarComFiltros(parametros: ParametrosFiltros): Promise<ResultadoConsultaComFiltros> {
		return this.transporte.json({
			metodo: "POST",
			caminho: "NFSe/ConsultarComFiltros",
			corpo: { pagina: 1, limiteDaPagina: 50, ...parametros },
			permiteRetry: true,
		});
	}

	/** Percorre todas as páginas de `consultarComFiltros`, nota a nota. */
	async *paginas(parametros: ParametrosFiltros): AsyncGenerator<NotaFiscal> {
		const limite = parametros.limiteDaPagina ?? 50;
		for (let pagina = Math.max(1, parametros.pagina ?? 1); ; pagina++) {
			const resultado = await this.consultarComFiltros({ ...parametros, pagina, limiteDaPagina: limite });
			yield* resultado.notas;
			if (resultado.notas.length === 0 || pagina >= Math.ceil(resultado.totalDeRegistros / Math.max(1, limite))) return;
		}
	}

	/** Consulta a nota de novo na prefeitura e atualiza a situação local. Repetir é seguro. */
	reconsultar(codigoNota: number): Promise<ProtocoloNotaResponse> {
		return this.ler(`NFSe/Reconsultar/${codigoNota}`);
	}

	/** Reconsulta pela chave de acesso do Emissor Nacional; atualiza a nota local de mesmo número. */
	reconsultarPorChaveAcesso(chaveAcesso: string, enviarParaProducao: boolean): Promise<ProtocoloNotaResponse> {
		return this.transporte.json({
			metodo: "POST",
			caminho: `NFSe/Reconsultar/ChaveAcesso/${seg(chaveAcesso)}`,
			corpo: { enviarParaProducao },
			permiteRetry: true,
		});
	}

	/** `POST /NFSe/Cancelar/{codigo}`. Assíncrono como a emissão; não retentado. */
	cancelar(codigoNota: number, opcoes: { urlWebhook?: string } = {}): Promise<ProtocoloResponse> {
		return this.transporte.json({ metodo: "POST", caminho: `NFSe/Cancelar/${codigoNota}`, corpo: opcoes, permiteRetry: false });
	}

	/** Bytes do XML; gravar em disco fica com você. */
	baixarXml(codigoNota: number): Promise<Uint8Array> {
		return this.transporte.bytes(`NFSe/XML/${codigoNota}`);
	}

	baixarPdf(codigoNota: number): Promise<Uint8Array> {
		return this.transporte.bytes(`NFSe/PDF/${codigoNota}`);
	}

	/** O `.xlsx` modelo de `importarPlanilha`. */
	baixarModeloPlanilha(): Promise<Uint8Array> {
		return this.transporte.bytes("NFSe/ModeloPlanilha");
	}

	/**
	 * Emite as notas de uma planilha no formato do modelo, com os padrões fiscais do emitente (que precisa do
	 * controle automático de RPS). Um protocolo por lote e os erros por linha. Não retentado (emite notas).
	 */
	async importarPlanilha(
		identificacaoEmitente: string,
		planilha: Arquivo,
		enviarParaProducao: boolean,
		urlWebhook?: string,
	): Promise<ResultadoImportacaoPlanilha> {
		const corpo = { arquivoBase64: await lerArquivo(planilha), identificacaoEmitente, enviarParaProducao, urlWebhook };
		return this.transporte.json({ metodo: "POST", caminho: "NFSe/ImportarPlanilha", corpo, permiteRetry: false });
	}

	private ler<T>(caminho: string): Promise<T> {
		return this.transporte.json({ metodo: "GET", caminho, permiteRetry: true });
	}
}

/** CRUD de emitente e upload do certificado A1 (rota `Emitente/*`). */
export class Emitentes {
	constructor(private readonly transporte: Transporte) {}

	/** Até 50 emitentes da conta. */
	consultar(): Promise<Emitente[]> {
		return this.ler("Emitente/Consultar");
	}

	consultarPorCodigo(codigo: number): Promise<Emitente> {
		return this.ler(`Emitente/Consultar/${codigo}`);
	}

	consultarPorCpfCnpj(cpfCnpj: string): Promise<Emitente> {
		return this.ler(`Emitente/ConsultarCpfCnpj/${seg(cpfCnpj)}`);
	}

	consultarPorTermo(termo: string): Promise<Emitente[]> {
		return this.ler(`Emitente/ConsultaPorTermo/${seg(termo)}`);
	}

	/** Devolve o código do emitente criado. */
	cadastrar(emitente: Emitente): Promise<ResultadoGravacaoEmitente> {
		return this.transporte.json({ metodo: "POST", caminho: "Emitente/Cadastrar", corpo: emitente, permiteRetry: false });
	}

	/** Grava todos os campos, inclusive os padrões fiscais: parta do emitente consultado (ver `Emitente`). */
	atualizar(emitente: Emitente): Promise<ResultadoGravacaoEmitente> {
		return this.transporte.json({ metodo: "PUT", caminho: "Emitente/Atualizar", corpo: emitente, permiteRetry: false });
	}

	excluir(codigo: number): Promise<Mensagem> {
		return this.transporte.json({ metodo: "DELETE", caminho: `Emitente/Excluir/${codigo}`, permiteRetry: false });
	}

	/** Envia o `.pfx` (caminho, bytes ou stream), convertido para Base64 aqui. */
	async uploadCertificado(cnpjEmissor: string, certificadoPfx: Arquivo, senha: string): Promise<ResultadoUploadCertificado> {
		const corpo = { cnpjEmissor, base64: await lerArquivo(certificadoPfx), senha };
		return this.transporte.json({ metodo: "POST", caminho: "Emitente/UploadCertificado", corpo, permiteRetry: false });
	}

	private ler<T>(caminho: string): Promise<T> {
		return this.transporte.json({ metodo: "GET", caminho, permiteRetry: true });
	}
}

/** CRUD e teste de webhook (fisicamente em `Emitente/Webhook*`). */
export class Webhooks {
	constructor(private readonly transporte: Transporte) {}

	obter(codigo: string): Promise<Webhook> {
		return this.transporte.json({ metodo: "GET", caminho: `Emitente/Webhook/${seg(codigo)}`, permiteRetry: true });
	}

	listarPorEmitente(codigoEmitente: number): Promise<Webhook[]> {
		return this.transporte.json({ metodo: "GET", caminho: `Emitente/Webhooks/${codigoEmitente}`, permiteRetry: true });
	}

	/** Não retentado: a API não barra URL duplicada, e um retry cego criaria dois registros. */
	criar(webhook: Webhook): Promise<Webhook> {
		return this.transporte.json({ metodo: "POST", caminho: "Emitente/Webhook", corpo: { ativo: true, ...webhook }, permiteRetry: false });
	}

	atualizar(webhook: Webhook): Promise<Webhook> {
		return this.transporte.json({ metodo: "PUT", caminho: "Emitente/Webhook", corpo: webhook, permiteRetry: false });
	}

	excluir(codigo: string): Promise<Mensagem> {
		return this.transporte.json({ metodo: "DELETE", caminho: `Emitente/Webhook/${seg(codigo)}`, permiteRetry: false });
	}

	/** Dispara um payload de exemplo do evento para a URL, sem precisar de webhook cadastrado. */
	testar(teste: { url: string; evento: WebhookEvento }): Promise<Mensagem> {
		return this.transporte.json({ metodo: "POST", caminho: "Emitente/Webhook/Teste", corpo: teste, permiteRetry: false });
	}
}

/** Municípios já homologados (`CoberturaNacional/MunicipiosHomologados`). */
export class Municipios {
	constructor(private readonly transporte: Transporte) {}

	/** Pelo código IBGE ou por um termo (parte do nome ou do código); sem nenhum dos dois, vem vazio. Até 20. */
	homologados(filtro: { codigoIbge?: number; termoDeBusca?: string }): Promise<MunicipioHomologado[]> {
		const query = new URLSearchParams();
		if (filtro.codigoIbge !== undefined) query.set("codigo", String(filtro.codigoIbge));
		if (filtro.termoDeBusca) query.set("termoDeBusca", filtro.termoDeBusca);
		const sufixo = query.toString() ? `?${query}` : "";
		return this.transporte.json({ metodo: "GET", caminho: `CoberturaNacional/MunicipiosHomologados${sufixo}`, permiteRetry: true });
	}
}
