<?php

declare(strict_types=1);

namespace Almztech\Sdk\Erros;

/** Erro devolvido pela API (`{"mensagem": "..."}`). Não há código estruturado: `mensagem` é texto livre. */
class AlmztechApiException extends AlmztechException
{
    public function __construct(
        public readonly int $statusCode,
        public readonly string $mensagem,
        public readonly ?string $corpoBruto = null,
    ) {
        parent::__construct("A API da Almztech retornou {$statusCode}: {$mensagem}", $statusCode);
    }
}
