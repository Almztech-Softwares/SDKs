package com.almztech.sdk;

/**
 * Client raiz do SDK Java da Almztech (DESIGN.md). Thread-safe: crie um por aplicação e reaproveite. A api key
 * está em "Primeiros passos" no Portal; nunca guarde login e senha no seu sistema.
 *
 * <pre>{@code
 * AlmztechClient client = new AlmztechClient(System.getenv("ALMZTECH_API_KEY"));
 * ProtocoloResponse protocolo = client.notasFiscais().emitir(lote);
 * ProtocoloResponse resultado = client.notasFiscais().aguardarConclusao(protocolo.protocolo);
 * }</pre>
 */
public final class AlmztechClient {
	private final NotasFiscaisClient notasFiscais;
	private final EmitentesClient emitentes;
	private final WebhooksClient webhooks;
	private final MunicipiosClient municipios;

	public AlmztechClient(String apiKey) {
		this(apiKey, new AlmztechClientOptions());
	}

	public AlmztechClient(String apiKey, AlmztechClientOptions opcoes) {
		Transporte transporte = new Transporte(apiKey, opcoes);
		this.notasFiscais = new NotasFiscaisClient(transporte);
		this.emitentes = new EmitentesClient(transporte);
		this.webhooks = new WebhooksClient(transporte);
		this.municipios = new MunicipiosClient(transporte);
	}

	public NotasFiscaisClient notasFiscais() { return notasFiscais; }
	public EmitentesClient emitentes() { return emitentes; }
	public WebhooksClient webhooks() { return webhooks; }
	public MunicipiosClient municipios() { return municipios; }

	public static String versao() { return Transporte.VERSAO_SDK; }
}
