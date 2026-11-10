<?php

declare(strict_types=1);

namespace Almztech\Sdk;

/** Envio HTTP com os streams nativos do PHP (`file_get_contents` + contexto), sem exigir ext-curl. */
final class EnvioHttpStream implements EnvioHttp
{
    public function enviar(string $metodo, string $url, array $headers, ?string $corpo, float $timeout): array
    {
        $linhas = [];
        foreach ($headers as $nome => $valor) {
            $linhas[] = "{$nome}: {$valor}";
        }

        $contexto = stream_context_create([
            'http' => [
                'method' => $metodo,
                'header' => implode("\r\n", $linhas),
                'content' => $corpo ?? '',
                'timeout' => $timeout,
                // Sem isto, 4xx/5xx viram aviso e o corpo com a mensagem da API se perde.
                'ignore_errors' => true,
                'follow_location' => 1,
            ],
        ]);

        $http_response_header = null;
        $conteudo = @file_get_contents($url, false, $contexto);
        if ($conteudo === false || $http_response_header === null) {
            $erro = error_get_last()['message'] ?? 'sem resposta';
            throw new \RuntimeException("Falha de rede: {$erro}");
        }

        $status = 0;
        $respostaHeaders = [];
        foreach ($http_response_header as $linha) {
            if (preg_match('#^HTTP/\S+\s+(\d{3})#', $linha, $partes)) {
                // Redirecionamentos acumulam vários blocos: vale o último.
                $status = (int) $partes[1];
                $respostaHeaders = [];
            } elseif (str_contains($linha, ':')) {
                [$nome, $valor] = explode(':', $linha, 2);
                $respostaHeaders[strtolower(trim($nome))] = trim($valor);
            }
        }

        return ['status' => $status, 'headers' => $respostaHeaders, 'corpo' => $conteudo];
    }
}
