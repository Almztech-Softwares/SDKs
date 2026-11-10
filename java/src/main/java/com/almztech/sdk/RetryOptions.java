package com.almztech.sdk;

import java.time.Duration;
import java.util.concurrent.ThreadLocalRandom;

/** Só operações seguras (leituras) são retentadas, e só em falha de rede ou 5xx (DESIGN.md seção 4). */
public class RetryOptions {
	private int tentativas = 3;
	private Duration intervaloInicial = Duration.ofMillis(500);
	private double multiplicador = 2.0;
	private Duration intervaloMaximo = Duration.ofSeconds(10);

	public int getTentativas() { return tentativas; }
	public RetryOptions tentativas(int tentativas) { this.tentativas = tentativas; return this; }
	public RetryOptions intervaloInicial(Duration intervalo) { this.intervaloInicial = intervalo; return this; }
	public RetryOptions multiplicador(double multiplicador) { this.multiplicador = multiplicador; return this; }
	public RetryOptions intervaloMaximo(Duration intervalo) { this.intervaloMaximo = intervalo; return this; }

	long atrasoEmMs(int tentativa) {
		double base = Math.min(intervaloInicial.toMillis() * Math.pow(multiplicador, tentativa - 1), intervaloMaximo.toMillis());
		return (long) (base + ThreadLocalRandom.current().nextDouble() * base * 0.2);
	}
}
