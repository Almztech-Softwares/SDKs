package com.almztech.sdk;

import com.almztech.sdk.erros.AlmztechWebhookParsingException;
import com.almztech.sdk.json.Json;
import com.almztech.sdk.modelos.ProtocoloResponse;
import com.almztech.sdk.modelos.StatusProtocolo;

/**
 * Leitura do payload recebido no seu endpoint de webhook (DESIGN.md seção 5). Não verifica autenticidade: a API
 * não assina o webhook. Proteger o endpoint (token secreto na URL, allowlist de IP, mTLS) é responsabilidade de quem o
 * recebe.
 */
public final class AlmztechWebhook {
	public enum TipoEvento { EMISSAO, CANCELAMENTO, REJEICAO }

	/** Evento classificado, com o payload completo (mesmo schema do polling). */
	public static final class Evento {
		public final TipoEvento tipo;
		public final ProtocoloResponse protocolo;

		Evento(TipoEvento tipo, ProtocoloResponse protocolo) {
			this.tipo = tipo;
			this.protocolo = protocolo;
		}
	}

	private AlmztechWebhook() {
	}

	/**
	 * {@code notas} não vazio → Emissão; senão {@code dadosCancelamento} → Cancelamento; senão status
	 * {@code REJEITADO} → Rejeição (motivo em {@code mensagem}); senão lança {@link AlmztechWebhookParsingException}.
	 */
	public static Evento ler(String corpo) {
		if (corpo == null || corpo.trim().isEmpty())
			throw new AlmztechWebhookParsingException("Corpo do webhook vazio.", null);

		ProtocoloResponse protocolo;
		try {
			protocolo = Json.ler(corpo, ProtocoloResponse.class);
		} catch (RuntimeException erro) {
			throw new AlmztechWebhookParsingException("Corpo do webhook não é um JSON válido.", erro);
		}

		if (protocolo.notas != null && !protocolo.notas.isEmpty())
			return new Evento(TipoEvento.EMISSAO, protocolo);
		if (protocolo.dadosCancelamento != null)
			return new Evento(TipoEvento.CANCELAMENTO, protocolo);
		if (protocolo.status == StatusProtocolo.REJEITADO)
			return new Evento(TipoEvento.REJEICAO, protocolo);
		throw new AlmztechWebhookParsingException("Payload de webhook não reconhecido (protocolo='" + protocolo.protocolo + "', status='"
				+ protocolo.status + "'): sem 'notas', sem 'dadosCancelamento' e status diferente de REJEITADO.", null);
	}
}
