package com.almztech.sdk.erros;

/** Falha de rede, timeout ou DNS antes de qualquer resposta HTTP. */
public class AlmztechNetworkException extends AlmztechException {
	public AlmztechNetworkException(String mensagem, Throwable causa) {
		super(mensagem, causa);
	}
}
