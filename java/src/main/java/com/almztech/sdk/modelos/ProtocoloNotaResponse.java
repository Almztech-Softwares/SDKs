package com.almztech.sdk.modelos;

import java.time.LocalDate;

/** Uma nota de um protocolo, ou o retorno das consultas e reconsultas de nota. */
public class ProtocoloNotaResponse {
	public int codigoNota;
	public String tipoNota;
	public String numero;
	public String rps;
	public String serie;
	public LocalDate dataEmissao;
	public StatusNota situacao;
	public String codigoVerificacao;
	public String chaveAcesso;
	/** URL de download do XML. */
	public String xml;
	/** URL de download do PDF. */
	public String pdf;
	public String dataHoraCancelamento;
	public String protocolo;
}
