package com.almztech.sdk.erros;

/** Base de todas as exceções do SDK (unchecked). Ver DESIGN.md seção 3. */
public class AlmztechException extends RuntimeException {
	public AlmztechException(String mensagem) {
		super(mensagem);
	}

	public AlmztechException(String mensagem, Throwable causa) {
		super(mensagem, causa);
	}
}
