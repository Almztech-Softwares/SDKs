package com.almztech.sdk;

import com.almztech.sdk.modelos.Mensagem;
import com.almztech.sdk.modelos.Webhook;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** CRUD e teste de webhook (fisicamente em {@code Emitente/Webhook*}). */
public class WebhooksClient {
	private final Transporte transporte;

	WebhooksClient(Transporte transporte) {
		this.transporte = transporte;
	}

	public Webhook obter(String codigo) {
		return transporte.json("GET", "Emitente/Webhook/" + Urls.segmento(codigo), null, true, Webhook.class);
	}

	public List<Webhook> listarPorEmitente(int codigoEmitente) {
		return transporte.json("GET", "Emitente/Webhooks/" + codigoEmitente, null, true, Tipos.listaDe(Webhook.class));
	}

	/** Não retentado: a API não barra URL duplicada, e um retry cego criaria dois registros. */
	public Webhook criar(Webhook webhook) {
		if (webhook.ativo == null)
			webhook.ativo = true;
		return transporte.json("POST", "Emitente/Webhook", webhook, false, Webhook.class);
	}

	public Webhook atualizar(Webhook webhook) {
		return transporte.json("PUT", "Emitente/Webhook", webhook, false, Webhook.class);
	}

	public Mensagem excluir(String codigo) {
		return transporte.json("DELETE", "Emitente/Webhook/" + Urls.segmento(codigo), null, false, Mensagem.class);
	}

	/** Dispara um payload de exemplo do evento ({@code WebhookEvento}) para a URL, sem precisar de webhook cadastrado. */
	public Mensagem testar(String url, int evento) {
		Map<String, Object> corpo = new LinkedHashMap<>();
		corpo.put("url", url);
		corpo.put("evento", evento);
		return transporte.json("POST", "Emitente/Webhook/Teste", corpo, false, Mensagem.class);
	}
}
