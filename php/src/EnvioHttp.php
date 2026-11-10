<?php

declare(strict_types=1);

namespace Almztech\Sdk;

/**
 * Envio HTTP injetável (proxy corporativo, instrumentação, testes). Deve devolver a resposta inclusive em 4xx/5xx e
 * lançar `\RuntimeException` só em falha de rede. O padrão é {@see EnvioHttpStream}, sem extensões além das nativas.
 */
interface EnvioHttp
{
    /**
     * @param array<string, string> $headers
     * @return array{status: int, headers: array<string, string>, corpo: string}
     */
    public function enviar(string $metodo, string $url, array $headers, ?string $corpo, float $timeout): array;
}
