package com.almztech.sdk.modelos;

import java.time.LocalDate;

/** Identificação do RPS. */
public class IdentificacaoDaNota {
	/** Opcional quando o emitente tem controle automático de RPS: a API numera. */
	public String numeroRPS;
	public String numeroNota;
	public String serie;
	/** Tipo do RPS (ex.: {@code "1"} = RPS). */
	public String tipo;
	/** 1 = normal (padrão da API), 2 = cancelado. */
	public Integer status;
	public LocalDate dataEmissao;
	public LocalDate dataCompetencia;
	public String codigoVerificacao;
	public String chaveNota;
}
