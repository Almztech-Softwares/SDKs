<?php

declare(strict_types=1);

namespace Almztech\Sdk;

use Almztech\Sdk\Erros\AlmztechPollingTimeoutException;
use Almztech\Sdk\Erros\AlmztechProtocoloRejeitadoException;

/**
 * Emissão, consulta, reconsulta, cancelamento, planilha e downloads de NFS-e (rota `NFSe/*`). Os dados são arrays
 * associativos com as chaves camelCase do JSON da API; chaves com `null` não são enviadas.
 */
final class NotasFiscais
{
    public function __construct(private readonly Transporte $transporte)
    {
    }

    /**
     * `POST /NFSe/Emitir`. Devolve só o protocolo em fila: acompanhe com {@see aguardarConclusao()} ou pelo webhook.
     * Produção exige licença válida e franquia disponível; homologação não consome franquia. **Nunca retentado
     * automaticamente** — a API não tem chave de idempotência e um reenvio pode duplicar a nota.
     *
     * @param array $lote `identificacaoEmitente`, `enviarParaProducao`, `notas` (cada uma com `rps`, `destinatario`,
     *                    `servicos`...), `urlWebhook`. Ver o README.
     */
    public function emitir(array $lote): array
    {
        return $this->transporte->json('POST', 'NFSe/Emitir', $lote);
    }

    /**
     * Consulta o protocolo com intervalos crescentes até um status terminal. `REJEITADO`/`ERRO_INTERNO` lançam
     * {@see AlmztechProtocoloRejeitadoException}; estourar `$timeoutTotal` (segundos) lança
     * {@see AlmztechPollingTimeoutException} com o último protocolo.
     *
     * @param (callable(array): void)|null $aCadaTentativa
     */
    public function aguardarConclusao(
        string $protocolo,
        float $intervaloInicial = 2.0,
        float $multiplicador = 1.5,
        float $intervaloMaximo = 30.0,
        float $timeoutTotal = 300.0,
        ?callable $aCadaTentativa = null,
    ): array {
        $inicio = microtime(true);
        $atraso = $intervaloInicial;
        while (true) {
            $ultimo = $this->consultarProtocolo($protocolo);
            if ($aCadaTentativa !== null) {
                $aCadaTentativa($ultimo);
            }

            $status = $ultimo['status'] ?? null;
            if ($status === 'PROCESSADO_COM_SUCESSO' || $status === 'CANCELADO') {
                return $ultimo;
            }
            if ($status === 'REJEITADO' || $status === 'ERRO_INTERNO') {
                throw new AlmztechProtocoloRejeitadoException($ultimo);
            }
            if (microtime(true) - $inicio >= $timeoutTotal) {
                throw new AlmztechPollingTimeoutException($protocolo, $ultimo);
            }

            usleep((int) ($atraso * 1_000_000));
            $atraso = min($atraso * $multiplicador, $intervaloMaximo);
        }
    }

    public function consultarProtocolo(string $protocolo): array
    {
        return $this->ler('NFSe/Consultar/Protocolo/' . rawurlencode($protocolo));
    }

    public function consultarPorCodigo(int $codigoNota): array
    {
        return $this->ler("NFSe/Consultar/{$codigoNota}");
    }

    /** Pelo número da NFS-e na prefeitura; havendo mais de uma entre os seus emitentes, vem a mais recente. */
    public function consultarPorNumero(string $numeroNota): array
    {
        return $this->ler('NFSe/Consultar/Numero/' . rawurlencode($numeroNota));
    }

    /** As 50 notas mais recentes do ambiente ({@see Filtros::PRODUCAO}/{@see Filtros::HOMOLOGACAO}). */
    public function consultarLista(string $ambiente): array
    {
        return $this->ler('NFSe/ConsultarLista/' . rawurlencode($ambiente));
    }

    /** Busca por CNPJ, código, número, razão social, nome fantasia ou protocolo (até 50). */
    public function consultarListaPorTermo(string $ambiente, string $termo): array
    {
        return $this->ler('NFSe/ConsultarListaPorTermo/' . rawurlencode($ambiente) . '/' . rawurlencode($termo));
    }

    /**
     * POST por causa do corpo, mas é leitura (retentado como os GET). Limite real de 100 por página. Devolve `notas`,
     * `paginaAtual`, `totalDeRegistros`, os totais por status e `contagensPorAba`.
     *
     * @param array $filtros Itens de {@see Filtros}.
     */
    public function consultarComFiltros(string $ambiente, array $filtros = [], ?string $filtroRapido = null, int $pagina = 1, int $limiteDaPagina = 50): array
    {
        $corpo = [
            'ambiente' => $ambiente,
            'filtroRapido' => $filtroRapido,
            'filtros' => $filtros === [] ? null : $filtros,
            'pagina' => $pagina,
            'limiteDaPagina' => $limiteDaPagina,
        ];
        return $this->transporte->json('POST', 'NFSe/ConsultarComFiltros', $corpo, true);
    }

    /**
     * Percorre todas as páginas de {@see consultarComFiltros()}, nota a nota, buscando cada página sob demanda.
     *
     * @return \Generator<int, array>
     */
    public function paginas(string $ambiente, array $filtros = [], ?string $filtroRapido = null, int $limiteDaPagina = 50): \Generator
    {
        for ($pagina = 1; ; $pagina++) {
            $resultado = $this->consultarComFiltros($ambiente, $filtros, $filtroRapido, $pagina, $limiteDaPagina);
            $notas = $resultado['notas'] ?? [];
            yield from $notas;
            if ($notas === [] || $pagina >= (int) ceil(($resultado['totalDeRegistros'] ?? 0) / max(1, $limiteDaPagina))) {
                return;
            }
        }
    }

    /** Consulta a nota de novo na prefeitura e atualiza a situação local. Repetir é seguro. */
    public function reconsultar(int $codigoNota): array
    {
        return $this->ler("NFSe/Reconsultar/{$codigoNota}");
    }

    /** Reconsulta pela chave de acesso do Emissor Nacional; atualiza a nota local de mesmo número. */
    public function reconsultarPorChaveAcesso(string $chaveAcesso, bool $enviarParaProducao): array
    {
        return $this->transporte->json('POST', 'NFSe/Reconsultar/ChaveAcesso/' . rawurlencode($chaveAcesso), ['enviarParaProducao' => $enviarParaProducao], true);
    }

    /** `POST /NFSe/Cancelar/{codigo}`. Assíncrono como a emissão; não retentado. */
    public function cancelar(int $codigoNota, ?string $urlWebhook = null): array
    {
        return $this->transporte->json('POST', "NFSe/Cancelar/{$codigoNota}", ['urlWebhook' => $urlWebhook]);
    }

    /** Bytes do XML; gravar em disco fica com você. */
    public function baixarXml(int $codigoNota): string
    {
        return $this->transporte->bytes("NFSe/XML/{$codigoNota}");
    }

    public function baixarPdf(int $codigoNota): string
    {
        return $this->transporte->bytes("NFSe/PDF/{$codigoNota}");
    }

    /** O `.xlsx` modelo de {@see importarPlanilha()}. */
    public function baixarModeloPlanilha(): string
    {
        return $this->transporte->bytes('NFSe/ModeloPlanilha');
    }

    /**
     * Emite as notas de uma planilha no formato do modelo, com os padrões fiscais do emitente (que precisa do controle
     * automático de RPS). Devolve `protocolos` (um por lote) e `erros` por linha. Não retentado (emite notas).
     *
     * @param string|resource $planilha Conteúdo binário do `.xlsx` ou um stream aberto. Para ler de um caminho, use
     *                                  `file_get_contents`.
     */
    public function importarPlanilha(string $identificacaoEmitente, mixed $planilha, bool $enviarParaProducao, ?string $urlWebhook = null): array
    {
        $corpo = [
            'arquivoBase64' => base64_encode(Arquivo::ler($planilha)),
            'identificacaoEmitente' => $identificacaoEmitente,
            'enviarParaProducao' => $enviarParaProducao,
            'urlWebhook' => $urlWebhook,
        ];
        return $this->transporte->json('POST', 'NFSe/ImportarPlanilha', $corpo);
    }

    private function ler(string $caminho): array
    {
        return $this->transporte->json('GET', $caminho, null, true);
    }
}
