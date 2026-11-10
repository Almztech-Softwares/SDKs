package com.almztech.sdk.erros;

/** 401 — token ausente, inválido ou revogado, ou licença inválida para produção. Só a mensagem diferencia. */
public class AlmztechAuthenticationException extends AlmztechApiException {
	public AlmztechAuthenticationException(String mensagem, String corpoBruto) {
		super(401, mensagem, corpoBruto);
	}
}
