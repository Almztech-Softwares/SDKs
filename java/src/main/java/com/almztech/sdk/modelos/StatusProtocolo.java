package com.almztech.sdk.modelos;

/** Status de um protocolo, pelo nome (idêntico ao JSON da API). */
public enum StatusProtocolo {
	EM_FILA,
	EM_PROCESSAMENTO,
	PENDENTE_PREFEITURA,
	PROCESSADO_COM_SUCESSO,
	REJEITADO,
	CANCELADO,
	ERRO_INTERNO
}
