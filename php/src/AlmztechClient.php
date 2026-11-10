<?php

declare(strict_types=1);

namespace Almztech\Sdk;

/**
 * Client raiz do SDK PHP da Almztech (DESIGN.md). Crie um por aplicação e reaproveite. A api key está em
 * "Primeiros passos" no Portal; nunca guarde login e senha no seu sistema.
 *
 * ```php
 * $client = new AlmztechClient(getenv('ALMZTECH_API_KEY'));
 * $protocolo = $client->notasFiscais->emitir($lote);
 * $resultado = $client->notasFiscais->aguardarConclusao($protocolo['protocolo']);
 * ```
 */
final class AlmztechClient
{
    public readonly NotasFiscais $notasFiscais;
    public readonly Emitentes $emitentes;
    public readonly Webhooks $webhooks;
    public readonly Municipios $municipios;

    public function __construct(string $apiKey, ?Opcoes $opcoes = null)
    {
        $transporte = new Transporte($apiKey, $opcoes ?? new Opcoes());
        $this->notasFiscais = new NotasFiscais($transporte);
        $this->emitentes = new Emitentes($transporte);
        $this->webhooks = new Webhooks($transporte);
        $this->municipios = new Municipios($transporte);
    }
}
