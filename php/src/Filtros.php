<?php

declare(strict_types=1);

namespace Almztech\Sdk;

/**
 * Filtros de `consultarComFiltros` (DESIGN.md seção 7). O valor vai sempre como texto: datas `Y-m-d` e
 * decimais com ponto. Campo desconhecido ou valor vazio é ignorado pela API (não filtra), por isso prefira as
 * constantes e os construtores daqui.
 */
final class Filtros
{
    /** Prefixo do protocolo das notas: o ambiente das listagens. */
    public const PRODUCAO = 'PRD';
    public const HOMOLOGACAO = 'HML';

    public const CAMPO_PROTOCOLO = 'protocolo';
    public const CAMPO_CODIGO_NOTA = 'codigoNota';
    public const CAMPO_NOME_EMITENTE = 'nomeEmitente';
    public const CAMPO_CPF_CNPJ_EMITENTE = 'cpfCnpjEmitente';
    public const CAMPO_NUMERO_RPS = 'numeroRps';
    public const CAMPO_SERIE_RPS = 'serieRps';
    public const CAMPO_LOTE_RPS = 'loteRps';
    public const CAMPO_VALOR_TOTAL = 'valorTotal';
    /** Data `Y-m-d`. */
    public const CAMPO_DATA_EMISSAO = 'dataEmissao';
    /** Vários status separados por `|`; só com `Igual`. */
    public const CAMPO_STATUS = 'status';

    // Operadores (OperadorFiltro da API, pelo nome).
    public const IGUAL = 'Igual';
    public const DIFERENTE = 'Diferente';
    public const MAIOR_QUE = 'MaiorQue';
    public const MAIOR_OU_IGUAL = 'MaiorOuIgual';
    public const MENOR_QUE = 'MenorQue';
    public const MENOR_OU_IGUAL = 'MenorOuIgual';
    public const ENTRE = 'Entre';
    public const CONTEM = 'Contem';
    public const INICIA_COM = 'IniciaCom';
    public const TERMINA_COM = 'TerminaCom';

    /** @return array{campo: string, operador: string, valor: string, valorFinal?: string} */
    public static function texto(string $campo, string $operador, string $valor): array
    {
        return ['campo' => $campo, 'operador' => $operador, 'valor' => $valor];
    }

    public static function numero(string $campo, string $operador, int|float|string $valor): array
    {
        return ['campo' => $campo, 'operador' => $operador, 'valor' => self::decimal($valor)];
    }

    public static function entreValores(string $campo, int|float|string $inicio, int|float|string $fim): array
    {
        return ['campo' => $campo, 'operador' => self::ENTRE, 'valor' => self::decimal($inicio), 'valorFinal' => self::decimal($fim)];
    }

    public static function data(string $operador, \DateTimeInterface|string $data): array
    {
        return ['campo' => self::CAMPO_DATA_EMISSAO, 'operador' => $operador, 'valor' => self::dataIso($data)];
    }

    public static function periodo(\DateTimeInterface|string $inicio, \DateTimeInterface|string $fim): array
    {
        return ['campo' => self::CAMPO_DATA_EMISSAO, 'operador' => self::ENTRE, 'valor' => self::dataIso($inicio), 'valorFinal' => self::dataIso($fim)];
    }

    public static function comStatus(string ...$status): array
    {
        return ['campo' => self::CAMPO_STATUS, 'operador' => self::IGUAL, 'valor' => implode('|', $status)];
    }

    private static function decimal(int|float|string $valor): string
    {
        // Independente do locale: sempre ponto decimal.
        return is_string($valor) ? $valor : rtrim(rtrim(sprintf('%.10F', $valor), '0'), '.');
    }

    private static function dataIso(\DateTimeInterface|string $data): string
    {
        return is_string($data) ? $data : $data->format('Y-m-d');
    }
}
