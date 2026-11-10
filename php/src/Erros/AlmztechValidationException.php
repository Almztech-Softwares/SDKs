<?php

declare(strict_types=1);

namespace Almztech\Sdk\Erros;

/** 422 (regra de negócio, inclusive franquia do plano esgotada) ou 400 (corpo recusado, com os erros por campo). */
class AlmztechValidationException extends AlmztechApiException
{
}
