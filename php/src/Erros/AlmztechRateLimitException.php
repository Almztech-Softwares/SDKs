<?php

declare(strict_types=1);

namespace Almztech\Sdk\Erros;

/** 429 — limite de requisições. Não é retentado sozinho: `retryAfter` (segundos) diz quanto esperar. */
class AlmztechRateLimitException extends AlmztechApiException
{
    public function __construct(string $mensagem, public readonly ?int $retryAfter, ?string $corpoBruto = null)
    {
        parent::__construct(429, $mensagem, $corpoBruto);
    }
}
