package com.almztech.sdk.modelos;

import java.util.List;

/** Resultado de {@code importarPlanilha}: um protocolo por lote e os erros por linha. */
public class ResultadoImportacaoPlanilha {
	public List<ProtocoloResponse> protocolos;
	public int totalLinhasLidas;
	public int totalNotasValidas;
	public List<ErroLinhaPlanilha> erros;
}
