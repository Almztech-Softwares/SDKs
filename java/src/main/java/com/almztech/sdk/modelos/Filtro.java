package com.almztech.sdk.modelos;

/** Critério de {@code consultarComFiltros}. Prefira os construtores de {@link Filtros}, que formatam o valor. */
public class Filtro {
	public String campo;
	/** Nome de {@link OperadorFiltro}. */
	public String operador;
	/** Sempre texto: datas {@code yyyy-MM-dd}, decimais com ponto. */
	public String valor;
	/** Fim do intervalo, só com {@code Entre}. */
	public String valorFinal;
}
