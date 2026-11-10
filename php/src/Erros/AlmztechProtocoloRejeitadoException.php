<?php

declare(strict_types=1);

namespace Almztech\Sdk\Erros;

/** O protocolo terminou em `REJEITADO` ou `ERRO_INTERNO`; o motivo está em `protocolo['mensagem']`. */
class AlmztechProtocoloRejeitadoException extends AlmztechException
{
    public function __construct(public readonly array $protocolo)
    {
        $mensagem = $protocolo['mensagem'] ?? '(sem mensagem)';
        parent::__construct("O protocolo {$protocolo['protocolo']} terminou em {$protocolo['status']}: {$mensagem}");
    }
}
