<?php

declare(strict_types=1);

namespace Almztech\Sdk\Erros;

/** 401 — token ausente, inválido ou revogado, ou licença inválida para produção. Só a mensagem diferencia. */
class AlmztechAuthenticationException extends AlmztechApiException
{
}
