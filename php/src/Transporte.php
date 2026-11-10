<?php

declare(strict_types=1);

namespace Almztech\Sdk;

use Almztech\Sdk\Erros\AlmztechApiException;
use Almztech\Sdk\Erros\AlmztechAuthenticationException;
use Almztech\Sdk\Erros\AlmztechAuthorizationException;
use Almztech\Sdk\Erros\AlmztechNetworkException;
use Almztech\Sdk\Erros\AlmztechRateLimitException;
use Almztech\Sdk\Erros\AlmztechServerException;
use Almztech\Sdk\Erros\AlmztechValidationException;

/** HTTP do SDK: header `x-api-key`, retry só em leituras, mapeamento de erros e JSON sem nulos. Uso interno. */
final class Transporte
{
    public const VERSAO_SDK = '0.2.0-beta.1';

    private readonly EnvioHttp $envio;
    private readonly string $baseUrl;

    public function __construct(private readonly string $apiKey, private readonly Opcoes $opcoes)
    {
        if (trim($apiKey) === '') {
            throw new \InvalidArgumentException('apiKey é obrigatório.');
        }
        $this->envio = $opcoes->envioHttp ?? new EnvioHttpStream();
        $this->baseUrl = rtrim($opcoes->baseUrl, '/');
        $this->log('AlmztechClient configurado (apiKey=' . LogSanitizer::sanitizarApiKey($apiKey) . ", baseUrl={$this->baseUrl})");
    }

    public function json(string $metodo, string $caminho, ?array $corpo = null, bool $permiteRetry = false): mixed
    {
        $resposta = $this->enviar($metodo, $caminho, $corpo, $permiteRetry);
        $this->log("<- {$resposta['status']} {$metodo} {$caminho} " . LogSanitizer::sanitizarCorpoJson($resposta['corpo']));
        if ($resposta['status'] >= 400) {
            throw self::mapearErro($resposta);
        }
        return trim($resposta['corpo']) === '' ? null : json_decode($resposta['corpo'], true, 512, JSON_THROW_ON_ERROR);
    }

    public function bytes(string $caminho): string
    {
        $resposta = $this->enviar('GET', $caminho, null, true);
        if ($resposta['status'] >= 400) {
            throw self::mapearErro($resposta);
        }
        return $resposta['corpo'];
    }

    /** @return array{status: int, headers: array<string, string>, corpo: string} */
    private function enviar(string $metodo, string $caminho, ?array $corpo, bool $permiteRetry): array
    {
        $tentativas = $permiteRetry ? max(1, $this->opcoes->tentativas) : 1;
        $json = $corpo === null ? null : json_encode(self::semNulos($corpo), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRESERVE_ZERO_FRACTION | JSON_THROW_ON_ERROR);
        $headers = [
            'x-api-key' => $this->apiKey,
            'User-Agent' => 'almztech-php-sdk/' . self::VERSAO_SDK,
            'Accept' => 'application/json',
        ];
        if ($json !== null) {
            $headers['Content-Type'] = 'application/json';
        }

        for ($tentativa = 1; ; $tentativa++) {
            if ($json !== null) {
                $this->log("-> {$metodo} {$caminho} " . LogSanitizer::sanitizarCorpoJson($json));
            }
            try {
                $resposta = $this->envio->enviar($metodo, "{$this->baseUrl}/{$caminho}", $headers, $json, $this->opcoes->timeout);
            } catch (\RuntimeException $erro) {
                if ($tentativa >= $tentativas) {
                    throw new AlmztechNetworkException("Falha de rede chamando {$metodo} {$caminho}: {$erro->getMessage()}", 0, $erro);
                }
                usleep($this->atrasoEmMicrossegundos($tentativa));
                continue;
            }

            if ($resposta['status'] >= 500 && $tentativa < $tentativas) {
                usleep($this->atrasoEmMicrossegundos($tentativa));
                continue;
            }
            return $resposta;
        }
    }

    private function atrasoEmMicrossegundos(int $tentativa): int
    {
        $base = min($this->opcoes->intervaloInicial * $this->opcoes->multiplicador ** ($tentativa - 1), $this->opcoes->intervaloMaximo);
        return (int) (($base + mt_rand() / mt_getrandmax() * $base * 0.2) * 1_000_000);
    }

    /**
     * Campo numérico da API (tipo não anulável) recusa null com 400: omitir deixa valer o padrão dela. Lista continua
     * lista, e objeto que ficou vazio continua objeto (`{}`, e não `[]`, que a API não aceita no lugar de um DTO).
     */
    private static function semNulos(array $dados): array|\stdClass
    {
        $resultado = [];
        foreach ($dados as $chave => $valor) {
            if ($valor === null) {
                continue;
            }
            $resultado[$chave] = is_array($valor) ? self::semNulos($valor) : $valor;
        }
        if (array_is_list($dados)) {
            return array_values($resultado);
        }
        return $resultado === [] ? new \stdClass() : $resultado;
    }

    private function log(string $mensagem): void
    {
        if ($this->opcoes->logger !== null) {
            ($this->opcoes->logger)($mensagem);
        }
    }

    /** @param array{status: int, headers: array<string, string>, corpo: string} $resposta */
    private static function mapearErro(array $resposta): AlmztechApiException
    {
        $status = $resposta['status'];
        $corpo = $resposta['corpo'];
        $mensagem = self::extrairMensagem($corpo);
        return match (true) {
            $status === 400, $status === 422 => new AlmztechValidationException($status, $mensagem, $corpo),
            $status === 401 => new AlmztechAuthenticationException($status, $mensagem, $corpo),
            $status === 403 => new AlmztechAuthorizationException($status, $mensagem, $corpo),
            $status === 429 => new AlmztechRateLimitException($mensagem, self::lerRetryAfter($resposta['headers']), $corpo),
            $status >= 500 => new AlmztechServerException($status, $mensagem, $corpo),
            default => new AlmztechApiException($status, $mensagem, $corpo),
        };
    }

    /** @param array<string, string> $headers */
    private static function lerRetryAfter(array $headers): ?int
    {
        foreach ($headers as $nome => $valor) {
            if (strtolower($nome) !== 'retry-after') {
                continue;
            }
            if (is_numeric($valor)) {
                return (int) $valor;
            }
            $data = strtotime($valor);
            return $data === false ? null : max(0, $data - time());
        }
        return null;
    }

    /** `{mensagem}` é o formato de erro da API; o 400 do ASP.NET vem como ProblemDetails, com `errors` por campo. */
    private static function extrairMensagem(string $corpo): string
    {
        if (trim($corpo) === '') {
            return '(sem corpo de resposta)';
        }
        $dados = json_decode($corpo, true);
        if (is_array($dados)) {
            if (isset($dados['mensagem']) && is_string($dados['mensagem'])) {
                return $dados['mensagem'];
            }
            if (isset($dados['errors']) && is_array($dados['errors'])) {
                $mensagens = [];
                foreach ($dados['errors'] as $campo => $erros) {
                    foreach ((array) $erros as $erro) {
                        $mensagens[] = "{$campo}: {$erro}";
                    }
                }
                return implode('; ', $mensagens);
            }
        }
        return $corpo;
    }
}
