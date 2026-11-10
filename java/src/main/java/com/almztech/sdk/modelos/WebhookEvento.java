package com.almztech.sdk.modelos;

/** Evento de webhook. A API o serializa como número, por isso são constantes e não um enum. */
public final class WebhookEvento {
	public static final int EMISSAO = 0;
	public static final int CANCELAMENTO = 1;
	public static final int REJEICAO = 2;

	private WebhookEvento() {
	}
}
