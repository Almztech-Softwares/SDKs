package com.almztech.sdk;

import com.almztech.sdk.json.Json;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * Mascaramento central de log (DESIGN.md seção 6). Cobre request e response: {@code senhaCertificado} e
 * {@code senhaPrefeitura} vêm em claro inclusive nas leituras de emitente. Todo log automático do SDK passa por aqui.
 */
public final class LogSanitizer {
	private static final Set<String> CAMPOS_SENSIVEIS = new HashSet<>(Arrays.asList(
			"apikey", "senha", "senhacertificado", "senhaprefeitura", "base64", "arquivobase64", "login"));
	private static final String MASCARA = "***";

	private LogSanitizer() {
	}

	/** Mostra só os últimos 4 caracteres da api key. */
	public static String sanitizarApiKey(String apiKey) {
		if (apiKey == null || apiKey.isEmpty())
			return MASCARA;
		return MASCARA + (apiKey.length() > 4 ? apiKey.substring(apiKey.length() - 4) : apiKey);
	}

	/** Cópia do JSON com todo campo sensível mascarado. Corpo não-JSON nunca é devolvido cru. */
	public static String sanitizarCorpoJson(String corpo) {
		if (corpo == null || corpo.trim().isEmpty())
			return "";
		try {
			return Json.escrever(mascarar(Json.ler(corpo)));
		} catch (RuntimeException erro) {
			return "<corpo não-JSON omitido da sanitização de log>";
		}
	}

	@SuppressWarnings("unchecked")
	private static Object mascarar(Object valor) {
		if (valor instanceof List) {
			List<Object> lista = new ArrayList<>();
			for (Object item : (List<Object>) valor)
				lista.add(mascarar(item));
			return lista;
		}
		if (valor instanceof Map) {
			Map<String, Object> mapa = new LinkedHashMap<>();
			for (Map.Entry<String, Object> item : ((Map<String, Object>) valor).entrySet())
				mapa.put(item.getKey(), CAMPOS_SENSIVEIS.contains(item.getKey().toLowerCase()) ? MASCARA : mascarar(item.getValue()));
			return mapa;
		}
		return valor;
	}
}
