# almztech/sdk (PHP)

SDK oficial da [Almztech](https://almztech.com.br) para PHP. Ele cobre a API de NFS-e:
- emissão, consulta, reconsulta e cancelamento de notas, inclusive por planilha;
- emitentes e certificados;
- webhooks;
- municípios homologados.

> Versão beta: a API pública do SDK ainda pode mudar até a 1.0.

## Instalação

```bash
composer require almztech/sdk:^0.2@beta
```

- PHP 8.1+.
- **Sem dependências**, nem de extensões além de `json`: o HTTP padrão usa os streams nativos. Dá para injetar outro envio, por exemplo com cURL ou Guzzle, implementando `EnvioHttp` e passando em `new Opcoes(envioHttp: ...)`.
- Os dados são arrays associativos no formato exato do JSON da API, em camelCase. Chaves com `null` não são enviadas.

## Uso

```php
use Almztech\Sdk\AlmztechClient;
use Almztech\Sdk\Erros\AlmztechProtocoloRejeitadoException;
use Almztech\Sdk\Erros\AlmztechValidationException;
use Almztech\Sdk\Filtros;
use Almztech\Sdk\Webhook;

// A api key está no Portal (portal.almztech.com.br), em Primeiros passos. Guarde-a na configuração do seu
// sistema, nunca no código.
$client = new AlmztechClient(getenv('ALMZTECH_API_KEY'));

try {
    $protocolo = $client->notasFiscais->emitir([
        'identificacaoEmitente' => '12345678000190',
        'enviarParaProducao' => false, // homologação (HML-...): não consome a franquia
        'notas' => [[
            'rps' => ['numeroRPS' => '1001', 'serie' => '1', 'tipo' => '1', 'dataEmissao' => date('Y-m-d')],
            'destinatario' => [
                'cpfCnpj' => '11222333000181',
                'razaoSocial' => 'Cliente Exemplo Ltda',
                'endereco' => ['cep' => '50030170', 'codigoIBGEMunicipio' => 2611606],
            ],
            'servicos' => [['codigo' => '0105', 'descricao' => 'Licenciamento de software', 'quantidade' => 1,
                            'valorUnitario' => 1500.00, 'aliquotaISS' => 2, 'retemISS' => 2]],
        ]],
    ]);

    $resultado = $client->notasFiscais->aguardarConclusao($protocolo['protocolo']);
    foreach ($resultado['notas'] ?? [] as $nota) {
        file_put_contents("nfse-{$nota['numero']}.pdf", $client->notasFiscais->baixarPdf($nota['codigoNota']));
    }
} catch (AlmztechProtocoloRejeitadoException $erro) {
    echo 'Rejeitada: ', $erro->protocolo['mensagem'] ?? '', "\n";
} catch (AlmztechValidationException $erro) {
    echo 'Recusada: ', $erro->mensagem, "\n";
}

// No seu endpoint de webhook (a API não assina o payload: proteja a URL):
$evento = Webhook::ler(file_get_contents('php://input')); // ['tipo' => Webhook::EMISSAO|CANCELAMENTO|REJEICAO, 'protocolo' => ...]
```

Consulta com filtros e paginação automática:

```php
foreach ($client->notasFiscais->paginas(Filtros::PRODUCAO, [
    Filtros::periodo('2026-10-01', '2026-10-31'),
    Filtros::comStatus('EMITIDA'),
]) as $nota) {
    echo $nota['numeroNota'], ' ', $nota['valorTotal'], "\n";
}
```

Exceções (`Almztech\Sdk\Erros`):
- `AlmztechValidationException` (422 e 400);
- `AlmztechAuthenticationException` (401);
- `AlmztechAuthorizationException` (403);
- `AlmztechRateLimitException` (429, com `retryAfter`);
- `AlmztechServerException` (5xx);
- `AlmztechNetworkException`;
- `AlmztechPollingTimeoutException`;
- `AlmztechProtocoloRejeitadoException`;
- `AlmztechWebhookParsingException`.

Só as leituras são retentadas automaticamente. `emitir`, `cancelar` e `importarPlanilha` nunca são.

## Links

- [Documentação da API](https://docs.almztech.com.br)
- [Código-fonte](https://github.com/Almztech-Softwares/SDK-PHP). Os SDKs das outras linguagens (.NET, Node.js, Java, Python
  e Ruby) seguem o mesmo [contrato](https://github.com/Almztech-Softwares/SDKs/blob/main/DESIGN.md).

## Licença

MIT, © 2026 ALMZTECH SOFTWARES LTDA. Veja [LICENSE](https://github.com/Almztech-Softwares/SDK-PHP/blob/main/LICENSE).
