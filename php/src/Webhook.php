<?php

declare(strict_types=1);

namespace Almztech\Sdk;

use Almztech\Sdk\Erros\AlmztechWebhookParsingException;

/**
 * Leitura do payload recebido no seu endpoint de webhook (DESIGN.md seção 5). Não verifica autenticidade: a API
 * não assina o webhook. Proteger o endpoint (token secreto na URL, allowlist de IP, mTLS) é responsabilidade de quem o
 * recebe.
 */
final class Webhook
{
    public const EMISSAO = 'Emissao';
    public const CANCELAMENTO = 'Cancelamento';
    public const REJEICAO = 'Rejeicao';

    /**
     * `notas` não vazio → Emissão; senão `dadosCancelamento` → Cancelamento; senão status `REJEITADO` → Rejeição (motivo
     * em `mensagem`); senão lança {@see AlmztechWebhookParsingException}.
     *
     * @param string|array $corpo Corpo cru do POST (ex.: `file_get_contents('php://input')`) ou já decodificado.
     * @return array{tipo: string, protocolo: array}
     */
    public static function ler(string|array $corpo): array
    {
        if (is_string($corpo)) {
            if (trim($corpo) === '') {
                throw new AlmztechWebhookParsingException('Corpo do webhook vazio.');
            }
            try {
                $corpo = json_decode($corpo, true, 512, JSON_THROW_ON_ERROR);
            } catch (\JsonException $erro) {
                throw new AlmztechWebhookParsingException('Corpo do webhook não é um JSON válido.', 0, $erro);
            }
            if (!is_array($corpo)) {
                throw new AlmztechWebhookParsingException('Corpo do webhook não é um objeto.');
            }
        }

        if (!empty($corpo['notas'])) {
            return ['tipo' => self::EMISSAO, 'protocolo' => $corpo];
        }
        if (!empty($corpo['dadosCancelamento'])) {
            return ['tipo' => self::CANCELAMENTO, 'protocolo' => $corpo];
        }
        if (($corpo['status'] ?? null) === 'REJEITADO') {
            return ['tipo' => self::REJEICAO, 'protocolo' => $corpo];
        }
        $protocolo = $corpo['protocolo'] ?? '';
        $status = $corpo['status'] ?? '';
        throw new AlmztechWebhookParsingException(
            "Payload de webhook não reconhecido (protocolo='{$protocolo}', status='{$status}'): sem 'notas', sem 'dadosCancelamento' e status diferente de REJEITADO."
        );
    }
}
