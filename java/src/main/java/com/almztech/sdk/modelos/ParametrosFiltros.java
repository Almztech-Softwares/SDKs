package com.almztech.sdk.modelos;

import java.util.List;

/** Corpo de {@code POST /NFSe/ConsultarComFiltros}. */
public class ParametrosFiltros {
	/** {@link Ambiente#PRODUCAO} ou {@link Ambiente#HOMOLOGACAO} (obrigatório). */
	public String ambiente = Ambiente.PRODUCAO;
	/** Busca livre por código, CPF/CNPJ do emitente, número da nota/RPS, nome fantasia ou protocolo. */
	public String filtroRapido;
	public List<Filtro> filtros;
	public int pagina = 1;
	/** Máximo real de 100 (clamp no servidor). */
	public int limiteDaPagina = 50;
}
