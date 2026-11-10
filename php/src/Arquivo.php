<?php

declare(strict_types=1);

namespace Almztech\Sdk;

/** Conteúdo de `.pfx`/`.xlsx` a partir de uma string binária ou de um stream aberto. Uso interno. */
final class Arquivo
{
    /** @param string|resource $arquivo */
    public static function ler(mixed $arquivo): string
    {
        if (is_string($arquivo)) {
            return $arquivo;
        }
        if (is_resource($arquivo)) {
            $conteudo = stream_get_contents($arquivo);
            if ($conteudo === false) {
                throw new \RuntimeException('Não foi possível ler o arquivo.');
            }
            return $conteudo;
        }
        throw new \InvalidArgumentException('Informe o conteúdo binário do arquivo ou um stream aberto.');
    }
}
