<?php

declare(strict_types=1);

namespace Almztech\Sdk;

/**
 * CRUD de emitente e upload do certificado A1 (rota `Emitente/*`). `atualizar` grava todos os campos, inclusive os
 * padrões fiscais: parta do emitente consultado. As leituras trazem `senhaCertificado`/`senhaPrefeitura` em claro —
 * nunca logue esses arrays.
 */
final class Emitentes
{
    public function __construct(private readonly Transporte $transporte)
    {
    }

    /** Até 50 emitentes da conta. */
    public function consultar(): array
    {
        return $this->transporte->json('GET', 'Emitente/Consultar', null, true);
    }

    public function consultarPorCodigo(int $codigo): array
    {
        return $this->transporte->json('GET', "Emitente/Consultar/{$codigo}", null, true);
    }

    public function consultarPorCpfCnpj(string $cpfCnpj): array
    {
        return $this->transporte->json('GET', 'Emitente/ConsultarCpfCnpj/' . rawurlencode($cpfCnpj), null, true);
    }

    public function consultarPorTermo(string $termo): array
    {
        return $this->transporte->json('GET', 'Emitente/ConsultaPorTermo/' . rawurlencode($termo), null, true);
    }

    /** Devolve `mensagem`, `cpfCnpj` e o `codigo` do emitente criado. */
    public function cadastrar(array $emitente): array
    {
        return $this->transporte->json('POST', 'Emitente/Cadastrar', $emitente);
    }

    public function atualizar(array $emitente): array
    {
        return $this->transporte->json('PUT', 'Emitente/Atualizar', $emitente);
    }

    public function excluir(int $codigo): array
    {
        return $this->transporte->json('DELETE', "Emitente/Excluir/{$codigo}");
    }

    /**
     * Envia o `.pfx`, convertido para Base64 aqui. Devolve `chaveCertificado`, `vencimento` e `aviso`.
     *
     * @param string|resource $certificadoPfx Conteúdo binário ou stream aberto.
     */
    public function uploadCertificado(string $cnpjEmissor, mixed $certificadoPfx, string $senha): array
    {
        $corpo = ['cnpjEmissor' => $cnpjEmissor, 'base64' => base64_encode(Arquivo::ler($certificadoPfx)), 'senha' => $senha];
        return $this->transporte->json('POST', 'Emitente/UploadCertificado', $corpo);
    }
}
