package com.almztech.sdk.modelos;

/** Retorno de {@code cadastrar}/{@code atualizar} emitente. */
public class ResultadoGravacaoEmitente {
	public String mensagem;
	public String cpfCnpj;
	/** Código do emitente, usado nas demais chamadas. */
	public Integer codigo;
}
