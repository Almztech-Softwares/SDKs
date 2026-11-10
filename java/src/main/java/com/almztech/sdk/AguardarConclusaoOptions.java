package com.almztech.sdk;

import com.almztech.sdk.modelos.ProtocoloResponse;
import java.time.Duration;
import java.util.function.Consumer;

/** Opções de {@link NotasFiscaisClient#aguardarConclusao(String, AguardarConclusaoOptions)}. */
public class AguardarConclusaoOptions {
	public Duration intervaloInicial = Duration.ofSeconds(2);
	public double backoffMultiplicador = 1.5;
	public Duration intervaloMaximo = Duration.ofSeconds(30);
	public Duration timeoutTotal = Duration.ofMinutes(5);
	/** Chamado a cada consulta, com o protocolo recebido. */
	public Consumer<ProtocoloResponse> aCadaTentativa;
}
