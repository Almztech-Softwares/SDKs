package com.almztech.sdk.modelos;

import java.math.BigDecimal;
import java.time.LocalDate;

/** Item das listagens e de {@code consultarComFiltros}. */
public class NotaFiscal {
	public int codigoNota;
	/** Vazio enquanto a nota não foi autorizada. */
	public String numeroNota;
	public int codigoEmitente;
	public String cpfCnpjEmitente;
	public String nomeEmitente;
	public String numeroRPS;
	public String serieRPS;
	public int loteRPS;
	public BigDecimal valorTotal;
	public StatusNota status;
	public LocalDate dataAutorizacao;
	public String tempoDeProcessamento;
	public String protocolo;
	/** Chave de acesso (Emissor Nacional), quando houver. */
	public String chaveAcesso;
}
