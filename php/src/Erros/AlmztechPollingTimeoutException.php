<?php

declare(strict_types=1);

namespace Almztech\Sdk\Erros;

/** O polling estourou o tempo total; `ultimoProtocolo` permite continuar manualmente. */
class AlmztechPollingTimeoutException extends AlmztechException
{
    public function __construct(public readonly string $protocolo, public readonly ?array $ultimoProtocolo)
    {
        $status = $ultimoProtocolo['status'] ?? 'desconhecido';
        parent::__construct("O protocolo {$protocolo} não concluiu no tempo de espera (último status: {$status}).");
    }
}
