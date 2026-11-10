package com.almztech.sdk;

import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;

/** Codificação de segmentos e parâmetros de URL. Uso interno. */
final class Urls {
	private Urls() {
	}

	static String segmento(String valor) {
		return URLEncoder.encode(valor, StandardCharsets.UTF_8).replace("+", "%20");
	}
}
