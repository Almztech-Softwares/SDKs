package com.almztech.sdk.modelos;

import java.util.List;

/** Corpo de {@code POST /NFSe/Emitir}. Campos nulos não são enviados (a API aplica o padrão dela). */
public class RecepcaoLote {
	/** CPF/CNPJ do emitente cadastrado. */
	public String identificacaoEmitente;
	public List<NotaFiscalResumida> notas;
	/** {@code false} gera protocolo de homologação ({@code HML-}), {@code true} de produção ({@code PRD-}). É por lote. */
	public boolean enviarParaProducao;
	/** Webhook só deste lote, além dos cadastrados para o emitente. */
	public String urlWebhook;
}
