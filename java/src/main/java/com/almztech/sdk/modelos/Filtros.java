package com.almztech.sdk.modelos;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.Arrays;
import java.util.stream.Collectors;

/** Construtores de {@link Filtro} que formatam o valor como a API espera (texto, datas ISO, decimal com ponto). */
public final class Filtros {
	private Filtros() {
	}

	public static Filtro texto(String campo, OperadorFiltro operador, String valor) {
		return criar(campo, operador, valor, null);
	}

	public static Filtro numero(String campo, OperadorFiltro operador, BigDecimal valor) {
		return criar(campo, operador, valor.toPlainString(), null);
	}

	public static Filtro entreValores(String campo, BigDecimal inicio, BigDecimal fim) {
		return criar(campo, OperadorFiltro.Entre, inicio.toPlainString(), fim.toPlainString());
	}

	public static Filtro data(OperadorFiltro operador, LocalDate data) {
		return criar(CamposFiltro.DATA_EMISSAO, operador, data.toString(), null);
	}

	public static Filtro periodo(LocalDate inicio, LocalDate fim) {
		return criar(CamposFiltro.DATA_EMISSAO, OperadorFiltro.Entre, inicio.toString(), fim.toString());
	}

	public static Filtro comStatus(StatusNota... status) {
		String valor = Arrays.stream(status).map(Enum::name).collect(Collectors.joining("|"));
		return criar(CamposFiltro.STATUS, OperadorFiltro.Igual, valor, null);
	}

	private static Filtro criar(String campo, OperadorFiltro operador, String valor, String valorFinal) {
		Filtro filtro = new Filtro();
		filtro.campo = campo;
		filtro.operador = operador.name();
		filtro.valor = valor;
		filtro.valorFinal = valorFinal;
		return filtro;
	}
}
