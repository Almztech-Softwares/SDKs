package com.almztech.sdk.erros;

import com.almztech.sdk.modelos.ProtocoloResponse;

/** O protocolo terminou em {@code REJEITADO} ou {@code ERRO_INTERNO}; o motivo está em {@code getProtocolo().mensagem}. */
public class AlmztechProtocoloRejeitadoException extends AlmztechException {
	private final ProtocoloResponse protocolo;

	public AlmztechProtocoloRejeitadoException(ProtocoloResponse protocolo) {
		super("O protocolo " + protocolo.protocolo + " terminou em " + protocolo.status + ": " + (protocolo.mensagem == null ? "(sem mensagem)" : protocolo.mensagem));
		this.protocolo = protocolo;
	}

	public ProtocoloResponse getProtocolo() {
		return protocolo;
	}
}
