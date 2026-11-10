<?php

declare(strict_types=1);

namespace Almztech\Sdk;

/** CRUD e teste de webhook (fisicamente em `Emitente/Webhook*`). Um registro = um evento. */
final class Webhooks
{
    /** Eventos: a API os serializa como número. */
    public const EMISSAO = 0;
    public const CANCELAMENTO = 1;
    public const REJEICAO = 2;

    public function __construct(private readonly Transporte $transporte)
    {
    }

    public function obter(string $codigo): array
    {
        return $this->transporte->json('GET', 'Emitente/Webhook/' . rawurlencode($codigo), null, true);
    }

    public function listarPorEmitente(int $codigoEmitente): array
    {
        return $this->transporte->json('GET', "Emitente/Webhooks/{$codigoEmitente}", null, true);
    }

    /** Não retentado: a API não barra URL duplicada, e um retry cego criaria dois registros. */
    public function criar(int $codigoEmitente, int $evento, string $url, bool $ativo = true): array
    {
        $corpo = ['codigoEmitente' => $codigoEmitente, 'evento' => $evento, 'url' => $url, 'ativo' => $ativo];
        return $this->transporte->json('POST', 'Emitente/Webhook', $corpo);
    }

    public function atualizar(array $webhook): array
    {
        return $this->transporte->json('PUT', 'Emitente/Webhook', $webhook);
    }

    public function excluir(string $codigo): array
    {
        return $this->transporte->json('DELETE', 'Emitente/Webhook/' . rawurlencode($codigo));
    }

    /** Dispara um payload de exemplo do evento para a URL, sem precisar de webhook cadastrado. */
    public function testar(string $url, int $evento): array
    {
        return $this->transporte->json('POST', 'Emitente/Webhook/Teste', ['url' => $url, 'evento' => $evento]);
    }
}
