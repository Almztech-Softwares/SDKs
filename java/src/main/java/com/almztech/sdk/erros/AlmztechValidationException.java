package com.almztech.sdk.erros;

/** 422 (regra de negócio, inclusive franquia do plano esgotada) ou 400 (corpo recusado antes do controller, com os erros por campo). */
public class AlmztechValidationException extends AlmztechApiException {
	public AlmztechValidationException(int statusCode, String mensagem, String corpoBruto) {
		super(statusCode, mensagem, corpoBruto);
	}
}
