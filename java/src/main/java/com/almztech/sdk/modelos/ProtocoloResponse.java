package com.almztech.sdk.modelos;

import java.util.List;

/** Retorno de emissão, cancelamento e consulta de protocolo; também é o payload do webhook. */
public class ProtocoloResponse {
	public String protocolo;
	public StatusProtocolo status;
	public String mensagem;
	public List<ProtocoloNotaResponse> notas;
	public DadosCancelamento dadosCancelamento;
}
