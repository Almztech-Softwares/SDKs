package com.almztech.sdk;

import java.time.Duration;
import java.util.function.Consumer;

/** Configuração do {@link AlmztechClient}. Todos os campos têm padrão; os setters encadeiam. */
public class AlmztechClientOptions {
	/** Padrão: produção. O ambiente fiscal é por chamada ({@code enviarParaProducao}), não por URL. */
	private String baseUrl = "https://api.almztech.com.br";
	/** Timeout de rede por chamada. Não é o tempo de processamento da prefeitura. */
	private Duration timeout = Duration.ofSeconds(30);
	private RetryOptions retry = new RetryOptions();
	private EnvioHttp envioHttp;
	/** Recebe só mensagens já mascaradas. */
	private Consumer<String> logger;

	public String getBaseUrl() { return baseUrl; }
	public AlmztechClientOptions baseUrl(String baseUrl) { this.baseUrl = baseUrl; return this; }
	public Duration getTimeout() { return timeout; }
	public AlmztechClientOptions timeout(Duration timeout) { this.timeout = timeout; return this; }
	public RetryOptions getRetry() { return retry; }
	public AlmztechClientOptions retry(RetryOptions retry) { this.retry = retry; return this; }
	public EnvioHttp getEnvioHttp() { return envioHttp; }
	public AlmztechClientOptions envioHttp(EnvioHttp envioHttp) { this.envioHttp = envioHttp; return this; }
	public Consumer<String> getLogger() { return logger; }
	public AlmztechClientOptions logger(Consumer<String> logger) { this.logger = logger; return this; }
}
