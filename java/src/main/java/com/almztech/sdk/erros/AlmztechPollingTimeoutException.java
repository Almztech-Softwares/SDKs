package com.almztech.sdk.erros;

import com.almztech.sdk.modelos.ProtocoloResponse;

/** O polling estourou o tempo total; {@link #getUltimoProtocolo()} permite continuar manualmente. */
public class AlmztechPollingTimeoutException extends AlmztechException {
	private final ProtocoloResponse ultimoProtocolo;

	public AlmztechPollingTimeoutException(String protocolo, ProtocoloResponse ultimoProtocolo) {
		super("O protocolo " + protocolo + " não concluiu no tempo de espera (último status: " + (ultimoProtocolo == null ? "desconhecido" : ultimoProtocolo.status) + ").");
		this.ultimoProtocolo = ultimoProtocolo;
	}

	public ProtocoloResponse getUltimoProtocolo() {
		return ultimoProtocolo;
	}
}
