package com.almztech.sdk.erros;

import java.time.Duration;

/** 429 — limite de requisições. Não é retentado sozinho: {@link #getRetryAfter()} diz quanto esperar. */
public class AlmztechRateLimitException extends AlmztechApiException {
	private final Duration retryAfter;

	public AlmztechRateLimitException(String mensagem, Duration retryAfter, String corpoBruto) {
		super(429, mensagem, corpoBruto);
		this.retryAfter = retryAfter;
	}

	/** Nulo quando a API não informou. */
	public Duration getRetryAfter() {
		return retryAfter;
	}
}
