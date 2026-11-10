package com.almztech.sdk.modelos;

/** Campos aceitos por {@link Filtro#campo}. Campo desconhecido ou valor vazio é ignorado pela API (não filtra). */
public final class CamposFiltro {
	public static final String PROTOCOLO = "protocolo";
	public static final String CODIGO_NOTA = "codigoNota";
	public static final String NOME_EMITENTE = "nomeEmitente";
	public static final String CPF_CNPJ_EMITENTE = "cpfCnpjEmitente";
	public static final String NUMERO_RPS = "numeroRps";
	public static final String SERIE_RPS = "serieRps";
	public static final String LOTE_RPS = "loteRps";
	public static final String VALOR_TOTAL = "valorTotal";
	/** Data {@code yyyy-MM-dd}. */
	public static final String DATA_EMISSAO = "dataEmissao";
	/** Vários status separados por {@code |}; só com {@code Igual}. */
	public static final String STATUS = "status";

	private CamposFiltro() {
	}
}
