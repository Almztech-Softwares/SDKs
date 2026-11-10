package com.almztech.sdk;

import java.io.IOException;
import java.io.InputStream;
import java.io.UncheckedIOException;
import java.nio.file.Files;
import java.nio.file.Path;

/** Leitura de {@code .pfx}/{@code .xlsx} para os métodos que recebem bytes (o SDK converte para Base64). */
public final class Arquivos {
	private Arquivos() {
	}

	public static byte[] ler(Path caminho) {
		try {
			return Files.readAllBytes(caminho);
		} catch (IOException erro) {
			throw new UncheckedIOException(erro);
		}
	}

	public static byte[] ler(InputStream entrada) {
		try {
			return entrada.readAllBytes();
		} catch (IOException erro) {
			throw new UncheckedIOException(erro);
		}
	}
}
