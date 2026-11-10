import { AlmztechErroWebhook } from "./erros.js";
import type { ProtocoloResponse } from "./modelos.js";

export type TipoEventoWebhook = "Emissao" | "Cancelamento" | "Rejeicao";

export interface EventoWebhook {
	tipo: TipoEventoWebhook;
	/** Payload completo (mesmo schema do polling). */
	protocolo: ProtocoloResponse;
}

/**
 * Classifica o corpo recebido no seu endpoint de webhook (DESIGN.md seção 5): `notas` não vazio →
 * Emissão; senão `dadosCancelamento` → Cancelamento; senão status `REJEITADO` → Rejeição (motivo em
 * `mensagem`); senão lança `AlmztechErroWebhook`.
 *
 * Não verifica autenticidade: a API não assina o webhook. Proteger o endpoint (token secreto na URL,
 * allowlist de IP, mTLS) é responsabilidade de quem o recebe.
 */
export function lerWebhook(corpo: string | object): EventoWebhook {
	let protocolo: ProtocoloResponse;
	if (typeof corpo === "string") {
		if (!corpo.trim()) throw new AlmztechErroWebhook("Corpo do webhook vazio.");
		try {
			protocolo = JSON.parse(corpo);
		} catch (erro) {
			throw new AlmztechErroWebhook("Corpo do webhook não é um JSON válido.", { cause: erro });
		}
	} else {
		protocolo = corpo as ProtocoloResponse;
	}

	if (!protocolo || typeof protocolo !== "object") throw new AlmztechErroWebhook("Corpo do webhook não é um objeto.");
	if (protocolo.notas?.length) return { tipo: "Emissao", protocolo };
	if (protocolo.dadosCancelamento) return { tipo: "Cancelamento", protocolo };
	if (protocolo.status === "REJEITADO") return { tipo: "Rejeicao", protocolo };

	throw new AlmztechErroWebhook(
		`Payload de webhook não reconhecido (protocolo='${protocolo.protocolo}', status='${protocolo.status}'): sem 'notas', sem 'dadosCancelamento' e status diferente de REJEITADO.`,
	);
}
