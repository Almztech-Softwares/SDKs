package com.almztech.sdk.erros;

/** Erro devolvido pela API ({@code {"mensagem": "..."}}). Não há código estruturado: {@link #getMensagem()} é texto livre. */
public class AlmztechApiException extends AlmztechException {
	private final int statusCode;
	private final String mensagem;
	private final String corpoBruto;

	public AlmztechApiException(int statusCode, String mensagem, String corpoBruto) {
		super("A API da Almztech retornou " + statusCode + ": " + mensagem);
		this.statusCode = statusCode;
		this.mensagem = mensagem;
		this.corpoBruto = corpoBruto;
	}

	public int getStatusCode() {
		return statusCode;
	}

	public String getMensagem() {
		return mensagem;
	}

	public String getCorpoBruto() {
		return corpoBruto;
	}
}
