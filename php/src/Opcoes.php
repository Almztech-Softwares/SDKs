<?php

declare(strict_types=1);

namespace Almztech\Sdk;

/**
 * Configuração do {@see AlmztechClient}. Retry só em leituras, e só em falha de rede ou 5xx (DESIGN.md seção 4).
 */
final class Opcoes
{
    /**
     * @param string $baseUrl Padrão: produção. O ambiente fiscal é por chamada (`enviarParaProducao`), não por URL.
     * @param float $timeout Timeout de rede por chamada, em segundos. Não é o tempo de processamento da prefeitura.
     * @param (callable(string): void)|null $logger Recebe só mensagens já mascaradas.
     */
    public function __construct(
        public readonly string $baseUrl = 'https://api.almztech.com.br',
        public readonly float $timeout = 30.0,
        public readonly int $tentativas = 3,
        public readonly float $intervaloInicial = 0.5,
        public readonly float $multiplicador = 2.0,
        public readonly float $intervaloMaximo = 10.0,
        public readonly ?EnvioHttp $envioHttp = null,
        public readonly mixed $logger = null,
    ) {
    }
}
