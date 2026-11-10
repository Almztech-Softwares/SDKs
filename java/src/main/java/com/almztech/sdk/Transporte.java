package com.almztech.sdk;

import com.almztech.sdk.erros.AlmztechApiException;
import com.almztech.sdk.erros.AlmztechAuthenticationException;
import com.almztech.sdk.erros.AlmztechAuthorizationException;
import com.almztech.sdk.erros.AlmztechNetworkException;
import com.almztech.sdk.erros.AlmztechRateLimitException;
import com.almztech.sdk.erros.AlmztechServerException;
import com.almztech.sdk.erros.AlmztechValidationException;
import com.almztech.sdk.json.Json;
import java.io.IOException;
import java.lang.reflect.Type;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.time.ZonedDateTime;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** HTTP do SDK: header {@code x-api-key}, retry só em leituras, mapeamento de erros e JSON sem nulos. Uso interno. */
final class Transporte {
	static final String VERSAO_SDK = "0.2.0-beta.1";

	private final String apiKey;
	private final AlmztechClientOptions opcoes;
	private final EnvioHttp envio;
	private final String baseUrl;

	Transporte(String apiKey, AlmztechClientOptions opcoes) {
		if (apiKey == null || apiKey.trim().isEmpty())
			throw new IllegalArgumentException("apiKey é obrigatório.");
		this.apiKey = apiKey;
		this.opcoes = opcoes;
		this.envio = opcoes.getEnvioHttp() != null ? opcoes.getEnvioHttp() : envioPadrao();
		this.baseUrl = opcoes.getBaseUrl().replaceAll("/+$", "");
		log("AlmztechClient configurado (apiKey=" + LogSanitizer.sanitizarApiKey(apiKey) + ", baseUrl=" + baseUrl + ")");
	}

	<T> T json(String metodo, String caminho, Object corpo, boolean permiteRetry, Type tipo) {
		EnvioHttp.Resposta resposta = enviar(metodo, caminho, corpo, permiteRetry);
		String texto = new String(resposta.corpo, StandardCharsets.UTF_8);
		log("<- " + resposta.status + " " + metodo + " " + caminho + " " + LogSanitizer.sanitizarCorpoJson(texto));
		if (resposta.status >= 400)
			throw mapearErro(resposta, texto);
		return texto.trim().isEmpty() ? null : Json.ler(texto, tipo);
	}

	byte[] bytes(String caminho) {
		EnvioHttp.Resposta resposta = enviar("GET", caminho, null, true);
		if (resposta.status >= 400)
			throw mapearErro(resposta, new String(resposta.corpo, StandardCharsets.UTF_8));
		return resposta.corpo;
	}

	private EnvioHttp.Resposta enviar(String metodo, String caminho, Object corpo, boolean permiteRetry) {
		RetryOptions retry = opcoes.getRetry();
		int tentativas = permiteRetry ? Math.max(1, retry.getTentativas()) : 1;
		String json = corpo == null ? null : Json.escrever(corpo);
		byte[] dados = json == null ? null : json.getBytes(StandardCharsets.UTF_8);

		Map<String, String> headers = new LinkedHashMap<>();
		headers.put("x-api-key", apiKey);
		headers.put("User-Agent", "almztech-java-sdk/" + VERSAO_SDK);
		headers.put("Accept", "application/json");
		if (dados != null)
			headers.put("Content-Type", "application/json");

		for (int tentativa = 1; ; tentativa++) {
			if (json != null)
				log("-> " + metodo + " " + caminho + " " + LogSanitizer.sanitizarCorpoJson(json));
			EnvioHttp.Resposta resposta;
			try {
				resposta = envio.enviar(metodo, baseUrl + "/" + caminho, headers, dados, opcoes.getTimeout());
			} catch (InterruptedException erro) {
				Thread.currentThread().interrupt();
				throw new AlmztechNetworkException("Chamada interrompida: " + metodo + " " + caminho, erro);
			} catch (IOException erro) {
				if (tentativa >= tentativas)
					throw new AlmztechNetworkException("Falha de rede chamando " + metodo + " " + caminho + ": " + erro.getMessage(), erro);
				dormir(retry.atrasoEmMs(tentativa));
				continue;
			}

			if (resposta.status >= 500 && tentativa < tentativas) {
				dormir(retry.atrasoEmMs(tentativa));
				continue;
			}
			return resposta;
		}
	}

	private static void dormir(long ms) {
		try {
			Thread.sleep(ms);
		} catch (InterruptedException erro) {
			Thread.currentThread().interrupt();
			throw new AlmztechNetworkException("Espera do retry interrompida.", erro);
		}
	}

	private void log(String mensagem) {
		if (opcoes.getLogger() != null)
			opcoes.getLogger().accept(mensagem);
	}

	private static EnvioHttp envioPadrao() {
		HttpClient cliente = HttpClient.newBuilder().followRedirects(HttpClient.Redirect.NORMAL).build();
		return (metodo, url, headers, corpo, timeout) -> {
			HttpRequest.Builder requisicao = HttpRequest.newBuilder(URI.create(url)).timeout(timeout)
					.method(metodo, corpo == null ? HttpRequest.BodyPublishers.noBody() : HttpRequest.BodyPublishers.ofByteArray(corpo));
			for (Map.Entry<String, String> header : headers.entrySet())
				requisicao.setHeader(header.getKey(), header.getValue());
			HttpResponse<byte[]> resposta = cliente.send(requisicao.build(), HttpResponse.BodyHandlers.ofByteArray());
			return new EnvioHttp.Resposta(resposta.statusCode(), resposta.headers().map(), resposta.body());
		};
	}

	private static AlmztechApiException mapearErro(EnvioHttp.Resposta resposta, String corpo) {
		String mensagem = extrairMensagem(corpo);
		int status = resposta.status;
		if (status == 400 || status == 422)
			return new AlmztechValidationException(status, mensagem, corpo);
		if (status == 401)
			return new AlmztechAuthenticationException(mensagem, corpo);
		if (status == 403)
			return new AlmztechAuthorizationException(mensagem, corpo);
		if (status == 429)
			return new AlmztechRateLimitException(mensagem, lerRetryAfter(resposta.header("Retry-After")), corpo);
		if (status >= 500)
			return new AlmztechServerException(status, mensagem, corpo);
		return new AlmztechApiException(status, mensagem, corpo);
	}

	private static Duration lerRetryAfter(String valor) {
		if (valor == null || valor.trim().isEmpty())
			return null;
		try {
			return Duration.ofSeconds(Long.parseLong(valor.trim()));
		} catch (NumberFormatException naoNumerico) {
			try {
				Duration espera = Duration.between(ZonedDateTime.now(), ZonedDateTime.parse(valor, DateTimeFormatter.RFC_1123_DATE_TIME));
				return espera.isNegative() ? Duration.ZERO : espera;
			} catch (RuntimeException invalido) {
				return null;
			}
		}
	}

	/** {@code {mensagem}} é o formato de erro da API; o 400 do ASP.NET vem como ProblemDetails, com {@code errors} por campo. */
	@SuppressWarnings("unchecked")
	private static String extrairMensagem(String corpo) {
		if (corpo.trim().isEmpty())
			return "(sem corpo de resposta)";
		try {
			Object json = Json.ler(corpo);
			if (json instanceof Map) {
				Map<String, Object> objeto = (Map<String, Object>) json;
				if (objeto.get("mensagem") instanceof String)
					return (String) objeto.get("mensagem");
				if (objeto.get("errors") instanceof Map) {
					List<String> mensagens = new ArrayList<>();
					for (Map.Entry<String, Object> campo : ((Map<String, Object>) objeto.get("errors")).entrySet()) {
						Object erros = campo.getValue();
						for (Object erro : erros instanceof List ? (List<Object>) erros : List.of(erros))
							mensagens.add(campo.getKey() + ": " + erro);
					}
					return String.join("; ", mensagens);
				}
			}
		} catch (RuntimeException naoJson) {
			// corpo não é JSON
		}
		return corpo;
	}
}
