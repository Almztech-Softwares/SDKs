package com.almztech.sdk.erros;

/** 403 — papel sem permissão para o endpoint. */
public class AlmztechAuthorizationException extends AlmztechApiException {
	public AlmztechAuthorizationException(String mensagem, String corpoBruto) {
		super(403, mensagem, corpoBruto);
	}
}
