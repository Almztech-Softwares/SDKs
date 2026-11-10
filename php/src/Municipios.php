<?php

declare(strict_types=1);

namespace Almztech\Sdk;

/** Municípios já homologados (`CoberturaNacional/MunicipiosHomologados`). */
final class Municipios
{
    public function __construct(private readonly Transporte $transporte)
    {
    }

    /** Pelo código IBGE e/ou por um termo; sem nenhum dos dois vem vazio. Até 20 itens `['id' => <IBGE>, 'text' => ...]`. */
    public function homologados(?int $codigoIbge = null, ?string $termoDeBusca = null): array
    {
        $parametros = array_filter(['codigo' => $codigoIbge, 'termoDeBusca' => $termoDeBusca], fn ($valor) => $valor !== null && $valor !== '');
        $sufixo = $parametros === [] ? '' : '?' . http_build_query($parametros);
        return $this->transporte->json('GET', "CoberturaNacional/MunicipiosHomologados{$sufixo}", null, true);
    }
}
