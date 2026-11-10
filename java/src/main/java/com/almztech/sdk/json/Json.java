package com.almztech.sdk.json;

import java.lang.reflect.Field;
import java.lang.reflect.Modifier;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;
import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.Collection;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * JSON mínimo do SDK, para não impor Jackson/Gson (e conflitos de versão) a quem integra. Lê para
 * {@code Map}/{@code List}/{@code String}/{@code BigDecimal}/{@code Boolean} e converte para os modelos pelos
 * campos públicos, com o nome do campo igual à chave camelCase da API. Na escrita, campos nulos são omitidos:
 * a API aplica o padrão dela, e null num campo numérico seria recusado com 400. Uso interno do SDK.
 */
public final class Json {
	private Json() {
	}

	// ---------------- escrita ----------------

	public static String escrever(Object valor) {
		StringBuilder saida = new StringBuilder();
		escrever(valor, saida);
		return saida.toString();
	}

	private static void escrever(Object valor, StringBuilder saida) {
		if (valor == null) {
			saida.append("null");
		} else if (valor instanceof String) {
			escreverTexto((String) valor, saida);
		} else if (valor instanceof BigDecimal) {
			saida.append(((BigDecimal) valor).toPlainString());
		} else if (valor instanceof Number || valor instanceof Boolean) {
			saida.append(valor);
		} else if (valor instanceof Enum) {
			escreverTexto(((Enum<?>) valor).name(), saida);
		} else if (valor instanceof LocalDate) {
			escreverTexto(valor.toString(), saida);
		} else if (valor instanceof Map) {
			saida.append('{');
			boolean primeiro = true;
			for (Map.Entry<?, ?> item : ((Map<?, ?>) valor).entrySet()) {
				if (item.getValue() == null)
					continue;
				if (!primeiro)
					saida.append(',');
				escreverTexto(String.valueOf(item.getKey()), saida);
				saida.append(':');
				escrever(item.getValue(), saida);
				primeiro = false;
			}
			saida.append('}');
		} else if (valor instanceof Collection) {
			saida.append('[');
			boolean primeiro = true;
			for (Object item : (Collection<?>) valor) {
				if (!primeiro)
					saida.append(',');
				escrever(item, saida);
				primeiro = false;
			}
			saida.append(']');
		} else {
			Map<String, Object> campos = new LinkedHashMap<>();
			for (Field campo : camposPublicos(valor.getClass()))
				campos.put(campo.getName(), ler(campo, valor));
			escrever(campos, saida);
		}
	}

	private static void escreverTexto(String texto, StringBuilder saida) {
		saida.append('"');
		for (int i = 0; i < texto.length(); i++) {
			char c = texto.charAt(i);
			switch (c) {
				case '"': saida.append("\\\""); break;
				case '\\': saida.append("\\\\"); break;
				case '\n': saida.append("\\n"); break;
				case '\r': saida.append("\\r"); break;
				case '\t': saida.append("\\t"); break;
				default:
					if (c < 0x20)
						saida.append(String.format("\\u%04x", (int) c));
					else
						saida.append(c);
			}
		}
		saida.append('"');
	}

	// ---------------- leitura ----------------

	/** Lê o texto para {@code Map}/{@code List}/{@code String}/{@code BigDecimal}/{@code Boolean}/{@code null}. */
	public static Object ler(String texto) {
		Leitor leitor = new Leitor(texto);
		Object valor = leitor.valor();
		leitor.espacos();
		if (!leitor.fim())
			throw new IllegalArgumentException("JSON com conteúdo após o fim, na posição " + leitor.posicao);
		return valor;
	}

	/** Lê o texto e converte para o tipo (classe de modelo, {@code List<T>}, {@code Map<String, T>}...). */
	@SuppressWarnings("unchecked")
	public static <T> T ler(String texto, Type tipo) {
		return (T) converter(ler(texto), tipo);
	}

	@SuppressWarnings({ "unchecked", "rawtypes" })
	public static Object converter(Object no, Type tipo) {
		if (tipo instanceof ParameterizedType) {
			ParameterizedType parametrizado = (ParameterizedType) tipo;
			Class<?> bruto = (Class<?>) parametrizado.getRawType();
			if (no == null)
				return null;
			if (List.class.isAssignableFrom(bruto)) {
				List<Object> lista = new ArrayList<>();
				for (Object item : (List<?>) no)
					lista.add(converter(item, parametrizado.getActualTypeArguments()[0]));
				return lista;
			}
			if (Map.class.isAssignableFrom(bruto)) {
				Map<String, Object> mapa = new LinkedHashMap<>();
				for (Map.Entry<String, Object> item : ((Map<String, Object>) no).entrySet())
					mapa.put(item.getKey(), converter(item.getValue(), parametrizado.getActualTypeArguments()[1]));
				return mapa;
			}
			return converter(no, bruto);
		}

		Class<?> classe = (Class<?>) tipo;
		if (no == null)
			return padraoPrimitivo(classe);
		if (classe == Object.class)
			return no;
		if (classe == String.class)
			return no instanceof BigDecimal ? ((BigDecimal) no).toPlainString() : String.valueOf(no);
		if (classe == int.class || classe == Integer.class)
			return numero(no).intValue();
		if (classe == long.class || classe == Long.class)
			return numero(no).longValue();
		if (classe == double.class || classe == Double.class)
			return numero(no).doubleValue();
		if (classe == BigDecimal.class)
			return numero(no);
		if (classe == boolean.class || classe == Boolean.class)
			return no instanceof Boolean ? no : Boolean.parseBoolean(String.valueOf(no));
		if (classe == LocalDate.class) {
			String texto = String.valueOf(no);
			return LocalDate.parse(texto.length() > 10 ? texto.substring(0, 10) : texto);
		}
		if (classe.isEnum()) {
			for (Object constante : classe.getEnumConstants())
				if (((Enum) constante).name().equalsIgnoreCase(String.valueOf(no)))
					return constante;
			return null; // valor novo da API não quebra a leitura
		}
		if (!(no instanceof Map))
			throw new IllegalArgumentException("Esperado objeto JSON para " + classe.getSimpleName() + ", veio " + no.getClass().getSimpleName());

		Map<String, Object> objeto = (Map<String, Object>) no;
		try {
			Object instancia = classe.getDeclaredConstructor().newInstance();
			for (Field campo : camposPublicos(classe)) {
				Object valor = buscarSemCaixa(objeto, campo.getName());
				if (valor != null || !campo.getType().isPrimitive())
					campo.set(instancia, converter(valor, campo.getGenericType()));
			}
			return instancia;
		} catch (ReflectiveOperationException erro) {
			throw new IllegalStateException("Não foi possível criar " + classe.getName(), erro);
		}
	}

	private static Object buscarSemCaixa(Map<String, Object> objeto, String chave) {
		if (objeto.containsKey(chave))
			return objeto.get(chave);
		for (Map.Entry<String, Object> item : objeto.entrySet())
			if (item.getKey().equalsIgnoreCase(chave))
				return item.getValue();
		return null;
	}

	private static BigDecimal numero(Object no) {
		return no instanceof BigDecimal ? (BigDecimal) no : new BigDecimal(String.valueOf(no));
	}

	private static Object padraoPrimitivo(Class<?> classe) {
		if (classe == int.class)
			return 0;
		if (classe == long.class)
			return 0L;
		if (classe == double.class)
			return 0d;
		if (classe == boolean.class)
			return false;
		return null;
	}

	private static List<Field> camposPublicos(Class<?> classe) {
		List<Field> campos = new ArrayList<>();
		for (Field campo : classe.getFields())
			if (!Modifier.isStatic(campo.getModifiers()))
				campos.add(campo);
		return campos;
	}

	private static Object ler(Field campo, Object instancia) {
		try {
			return campo.get(instancia);
		} catch (IllegalAccessException erro) {
			throw new IllegalStateException(erro);
		}
	}

	private static final class Leitor {
		private final String texto;
		private int posicao;

		Leitor(String texto) {
			this.texto = texto;
		}

		boolean fim() {
			return posicao >= texto.length();
		}

		void espacos() {
			while (!fim() && Character.isWhitespace(texto.charAt(posicao)))
				posicao++;
		}

		Object valor() {
			espacos();
			if (fim())
				throw erro("JSON terminou antes do esperado");
			char c = texto.charAt(posicao);
			if (c == '{')
				return objeto();
			if (c == '[')
				return lista();
			if (c == '"')
				return texto();
			if (texto.startsWith("true", posicao)) {
				posicao += 4;
				return Boolean.TRUE;
			}
			if (texto.startsWith("false", posicao)) {
				posicao += 5;
				return Boolean.FALSE;
			}
			if (texto.startsWith("null", posicao)) {
				posicao += 4;
				return null;
			}
			return numero();
		}

		private Map<String, Object> objeto() {
			Map<String, Object> mapa = new LinkedHashMap<>();
			posicao++;
			espacos();
			if (texto.charAt(posicao) == '}') {
				posicao++;
				return mapa;
			}
			while (true) {
				espacos();
				String chave = texto();
				espacos();
				esperar(':');
				mapa.put(chave, valor());
				espacos();
				if (texto.charAt(posicao) == ',') {
					posicao++;
					continue;
				}
				esperar('}');
				return mapa;
			}
		}

		private List<Object> lista() {
			List<Object> lista = new ArrayList<>();
			posicao++;
			espacos();
			if (texto.charAt(posicao) == ']') {
				posicao++;
				return lista;
			}
			while (true) {
				lista.add(valor());
				espacos();
				if (texto.charAt(posicao) == ',') {
					posicao++;
					continue;
				}
				esperar(']');
				return lista;
			}
		}

		private String texto() {
			esperar('"');
			StringBuilder saida = new StringBuilder();
			while (true) {
				if (fim())
					throw erro("Texto sem aspas de fechamento");
				char c = texto.charAt(posicao++);
				if (c == '"')
					return saida.toString();
				if (c != '\\') {
					saida.append(c);
					continue;
				}
				char escape = texto.charAt(posicao++);
				switch (escape) {
					case 'n': saida.append('\n'); break;
					case 'r': saida.append('\r'); break;
					case 't': saida.append('\t'); break;
					case 'b': saida.append('\b'); break;
					case 'f': saida.append('\f'); break;
					case 'u':
						saida.append((char) Integer.parseInt(texto.substring(posicao, posicao + 4), 16));
						posicao += 4;
						break;
					default: saida.append(escape);
				}
			}
		}

		private BigDecimal numero() {
			int inicio = posicao;
			while (!fim() && "+-0123456789.eE".indexOf(texto.charAt(posicao)) >= 0)
				posicao++;
			if (inicio == posicao)
				throw erro("Valor JSON inválido");
			return new BigDecimal(texto.substring(inicio, posicao));
		}

		private void esperar(char c) {
			if (fim() || texto.charAt(posicao) != c)
				throw erro("Esperado '" + c + "'");
			posicao++;
		}

		private IllegalArgumentException erro(String mensagem) {
			return new IllegalArgumentException(mensagem + " na posição " + posicao);
		}
	}
}
