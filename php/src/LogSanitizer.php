<?php

declare(strict_types=1);

namespace Almztech\Sdk;

/**
 * Mascaramento central de log (DESIGN.md seção 6). Cobre request e response: `senhaCertificado` e
 * `senhaPrefeitura` vêm em claro inclusive nas leituras de emitente. Todo log automático do SDK passa por aqui.
 */
final class LogSanitizer
{
    private const CAMPOS_SENSIVEIS = ['apikey', 'senha', 'senhacertificado', 'senhaprefeitura', 'base64', 'arquivobase64', 'login'];
    private const MASCARA = '***';

    /** Mostra só os últimos 4 caracteres da api key. */
    public static function sanitizarApiKey(string $apiKey): string
    {
        if ($apiKey === '') {
            return self::MASCARA;
        }
        return self::MASCARA . (strlen($apiKey) > 4 ? substr($apiKey, -4) : $apiKey);
    }

    /** Cópia do JSON com todo campo sensível mascarado. Corpo não-JSON nunca é devolvido cru. */
    public static function sanitizarCorpoJson(?string $corpo): string
    {
        if ($corpo === null || trim($corpo) === '') {
            return '';
        }
        try {
            $dados = json_decode($corpo, true, 512, JSON_THROW_ON_ERROR);
        } catch (\JsonException) {
            return '<corpo não-JSON omitido da sanitização de log>';
        }
        return json_encode(self::mascarar($dados), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    }

    private static function mascarar(mixed $valor): mixed
    {
        if (!is_array($valor)) {
            return $valor;
        }
        $resultado = [];
        foreach ($valor as $chave => $item) {
            $resultado[$chave] = is_string($chave) && in_array(strtolower($chave), self::CAMPOS_SENSIVEIS, true)
                ? self::MASCARA
                : self::mascarar($item);
        }
        return $resultado;
    }
}
