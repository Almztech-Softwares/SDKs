package com.almztech.sdk.modelos;

import java.util.List;
import java.util.Map;

/** Página de {@code consultarComFiltros}. */
public class ResultadoConsultaComFiltros {
	public List<NotaFiscal> notas;
	public int paginaAtual;
	/** Total com todos os filtros, inclusive o de status. */
	public int totalDeRegistros;
	public int totalEmProcessamento;
	public int totalEmitidas;
	public int totalCanceladas;
	public int totalDenegadas;
	public int totalRejeitadas;
	public int totalPendentes;
	/** Por status, ignorando o próprio filtro de status (chave {@code all} = todos). */
	public Map<String, Integer> contagensPorAba;
}
