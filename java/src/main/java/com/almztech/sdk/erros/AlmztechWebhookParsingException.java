package com.almztech.sdk.erros;

/** Payload de webhook não reconhecido. */
public class AlmztechWebhookParsingException extends AlmztechException {
	public AlmztechWebhookParsingException(String mensagem, Throwable causa) {
		super(mensagem, causa);
	}
}
