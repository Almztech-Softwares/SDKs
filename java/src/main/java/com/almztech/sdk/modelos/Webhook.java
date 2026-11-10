package com.almztech.sdk.modelos;

/** Um registro = um evento ({@link WebhookEvento}); para vários eventos, cadastre vários registros. */
public class Webhook {
	public String codigo;
	/** Ver {@link WebhookEvento}. */
	public Integer evento;
	public Boolean ativo;
	public String url;
	public Integer codigoEmitente;
}
