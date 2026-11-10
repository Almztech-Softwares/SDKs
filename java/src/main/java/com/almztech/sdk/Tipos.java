package com.almztech.sdk;

import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;

/** {@code List<T>} e {@code Map<String, T>} como {@link Type}, para o {@code Json} converter as respostas. Uso interno. */
final class Tipos {
	private Tipos() {
	}

	static Type listaDe(Class<?> item) {
		return parametrizado(java.util.List.class, item);
	}

	private static Type parametrizado(Class<?> bruto, Type... argumentos) {
		return new ParameterizedType() {
			@Override
			public Type[] getActualTypeArguments() {
				return argumentos.clone();
			}

			@Override
			public Type getRawType() {
				return bruto;
			}

			@Override
			public Type getOwnerType() {
				return null;
			}
		};
	}
}
