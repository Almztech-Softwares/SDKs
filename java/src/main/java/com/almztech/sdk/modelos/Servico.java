package com.almztech.sdk.modelos;

import java.math.BigDecimal;

/** Item de serviço. Os códigos fiscais são números, como na API; os nulos usam o padrão dela. */
public class Servico {
	public String codigo;
	public String descricao;
	public String codigoTributacao;
	/** Código IBGE (7 dígitos) do município onde o serviço foi prestado. */
	public String localPrestacao;
	public Integer responsavelPelaRetencao;
	/** ABRASF: 1 Exigível, 2 Não incidência, 3 Isenção, 4 Exportação, 5 Imunidade, 6/7 Suspensa. */
	public Integer exigibilidadeISS;
	/** Varia conforme o município de prestação. */
	public Integer tipoTributacao;
	/** Código IBGE do município onde o ISS incide. */
	public Integer municipioIncidencia;
	public BigDecimal aliquotaISS;
	/** 1 = retido pelo tomador, 2 = não retido. */
	public Integer retemISS;
	/** Sem valor, a API assume 0: informe 1 para um item único. */
	public BigDecimal quantidade = BigDecimal.ONE;
	public BigDecimal valorUnitario;
	public BigDecimal valorDesconto;
	public BigDecimal valorAcrescimo;
	public String codigoCnae;
	public String numeroProcesso;
	public String nbs;
	/** Grupo IBS/CBS da Reforma Tributária. */
	public IbsCbs ibsCbs;
	public PisCofins pisCofins;
	public TotaisDosTributos totaisDosTributos;
}
