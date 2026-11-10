package com.almztech.sdk;

import java.io.IOException;
import java.time.Duration;
import java.util.List;
import java.util.Map;

/**
 * Envio HTTP injetável (proxy corporativo, instrumentação, testes). Deve devolver a resposta inclusive em 4xx/5xx
 * e lançar {@link IOException} só em falha de rede. O padrão usa {@code java.net.http.HttpClient}.
 */
@FunctionalInterface
public interface EnvioHttp {
	Resposta enviar(String metodo, String url, Map<String, String> headers, byte[] corpo, Duration timeout) throws IOException, InterruptedException;

	final class Resposta {
		public final int status;
		public final Map<String, List<String>> headers;
		public final byte[] corpo;

		public Resposta(int status, Map<String, List<String>> headers, byte[] corpo) {
			this.status = status;
			this.headers = headers;
			this.corpo = corpo;
		}

		/** Primeiro valor do header, sem diferenciar caixa. */
		public String header(String nome) {
			for (Map.Entry<String, List<String>> item : headers.entrySet())
				if (item.getKey() != null && item.getKey().equalsIgnoreCase(nome) && !item.getValue().isEmpty())
					return item.getValue().get(0);
			return null;
		}
	}
}
