package com.almztech.sdk.erros;

/** 5xx — erro interno; em produção a mensagem pode vir genérica. */
public class AlmztechServerException extends AlmztechApiException {
	public AlmztechServerException(int statusCode, String mensagem, String corpoBruto) {
		super(statusCode, mensagem, corpoBruto);
	}
}
