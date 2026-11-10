<?php

declare(strict_types=1);

/*
  Testes do SDK PHP contra os fixtures compartilhados (fixtures/) e o contrato atual da API. Runner próprio,
  sem PHPUnit, para rodar só com o PHP: `php tests/TestesDoSdk.php` a partir de php/. Toda função `teste*` é um
  teste.
*/

use Almztech\Sdk\AlmztechClient;
use Almztech\Sdk\EnvioHttp;
use Almztech\Sdk\Erros\AlmztechAuthenticationException;
use Almztech\Sdk\Erros\AlmztechAuthorizationException;
use Almztech\Sdk\Erros\AlmztechNetworkException;
use Almztech\Sdk\Erros\AlmztechPollingTimeoutException;
use Almztech\Sdk\Erros\AlmztechProtocoloRejeitadoException;
use Almztech\Sdk\Erros\AlmztechRateLimitException;
use Almztech\Sdk\Erros\AlmztechServerException;
use Almztech\Sdk\Erros\AlmztechValidationException;
use Almztech\Sdk\Erros\AlmztechWebhookParsingException;
use Almztech\Sdk\Filtros;
use Almztech\Sdk\LogSanitizer;
use Almztech\Sdk\Opcoes;
use Almztech\Sdk\Webhook;
use Almztech\Sdk\Webhooks;

spl_autoload_register(function (string $classe): void {
    $prefixo = 'Almztech\\Sdk\\';
    if (str_starts_with($classe, $prefixo)) {
        require __DIR__ . '/../src/' . str_replace('\\', '/', substr($classe, strlen($prefixo))) . '.php';
    }
});

final class EnvioFalso implements EnvioHttp
{
    /** @var list<array|\RuntimeException> */
    public array $fila = [];
    /** @var list<array{metodo: string, caminho: string, headers: array, corpo: ?string}> */
    public array $chamadas = [];

    public function responder(int $status, string $corpo = '', array $headers = []): self
    {
        $this->fila[] = ['status' => $status, 'headers' => $headers, 'corpo' => $corpo];
        return $this;
    }

    public function falharRede(): self
    {
        $this->fila[] = new \RuntimeException('rede indisponível');
        return $this;
    }

    public function enviar(string $metodo, string $url, array $headers, ?string $corpo, float $timeout): array
    {
        $this->chamadas[] = ['metodo' => $metodo, 'caminho' => str_replace('https://sdk-tests.local', '', $url), 'headers' => $headers, 'corpo' => $corpo];
        $resposta = array_shift($this->fila);
        if ($resposta instanceof \RuntimeException) {
            throw $resposta;
        }
        return $resposta;
    }

    public function corpo(int $indice = 0): array
    {
        return json_decode($this->chamadas[$indice]['corpo'], true);
    }
}

function client(EnvioFalso $envio, ?callable $logger = null, string $apiKey = 'api-key-de-teste'): AlmztechClient
{
    return new AlmztechClient($apiKey, new Opcoes(
        baseUrl: 'https://sdk-tests.local',
        intervaloInicial: 0.001,
        intervaloMaximo: 0.005,
        envioHttp: $envio,
        logger: $logger,
    ));
}

/** Payload do fixture como a API o enviaria (sem `_comentario`; erros guardam em `body`, listas em `response`). */
function fixture(string $caminho): string
{
    $dados = json_decode(file_get_contents(__DIR__ . '/../../fixtures/' . $caminho), true);
    unset($dados['_comentario']);
    return json_encode($dados['body'] ?? $dados['response'] ?? $dados);
}

function verificar(bool $condicao, string $mensagem = 'condição falsa'): void
{
    if (!$condicao) {
        throw new \AssertionError($mensagem);
    }
}

function igual(mixed $esperado, mixed $atual): void
{
    if ($esperado !== $atual) {
        throw new \AssertionError('esperado ' . var_export($esperado, true) . ' mas veio ' . var_export($atual, true));
    }
}

/** @template T of \Throwable @param class-string<T> $tipo @return T */
function lanca(string $tipo, callable $acao): \Throwable
{
    try {
        $acao();
    } catch (\Throwable $erro) {
        if ($erro instanceof $tipo) {
            return $erro;
        }
        throw new \AssertionError("esperado {$tipo} mas veio " . $erro::class . ': ' . $erro->getMessage());
    }
    throw new \AssertionError("esperado {$tipo}, nada foi lançado");
}

const EM_FILA = '{"protocolo":"HML-1","status":"EM_FILA"}';
const LOTE_VAZIO = ['identificacaoEmitente' => '1', 'notas' => [], 'enviarParaProducao' => false];

// ---------------- retry ----------------

function testeLeituraRetentaEm5xx(): void
{
    $envio = (new EnvioFalso())->responder(500)->responder(503)->responder(200, EM_FILA);
    igual('EM_FILA', client($envio)->notasFiscais->consultarProtocolo('HML-1')['status']);
    igual(3, count($envio->chamadas));
}

function testeLeituraDesisteAposFalhasDeRede(): void
{
    $envio = (new EnvioFalso())->falharRede()->falharRede()->falharRede();
    lanca(AlmztechNetworkException::class, fn () => client($envio)->notasFiscais->consultarProtocolo('HML-1'));
    igual(3, count($envio->chamadas));
}

function testeEmitirCancelarEImportarNuncaRetentam(): void
{
    $envio = (new EnvioFalso())->responder(500)->falharRede()->responder(500)->responder(500);
    $client = client($envio);
    lanca(AlmztechServerException::class, fn () => $client->notasFiscais->emitir(LOTE_VAZIO));
    lanca(AlmztechNetworkException::class, fn () => $client->notasFiscais->emitir(LOTE_VAZIO));
    lanca(AlmztechServerException::class, fn () => $client->notasFiscais->cancelar(7));
    lanca(AlmztechServerException::class, fn () => $client->notasFiscais->importarPlanilha('1', "\x01", false));
    igual(4, count($envio->chamadas));
    igual('{}', $envio->chamadas[2]['corpo']);
}

function teste4xxNaoRetenta(): void
{
    $envio = (new EnvioFalso())->responder(422, fixture('erros/422-validacao.json'));
    lanca(AlmztechValidationException::class, fn () => client($envio)->notasFiscais->consultarPorCodigo(999999));
    igual(1, count($envio->chamadas));
}

// ---------------- erros ----------------

function testeErrosTipadosComAMensagem(): void
{
    $envio = (new EnvioFalso())
        ->responder(401, fixture('erros/401-sem-token.json'))
        ->responder(403, fixture('erros/403-proibido.json'))
        ->responder(422, fixture('erros/422-validacao.json'));
    $client = client($envio);
    igual('Token não informado', lanca(AlmztechAuthenticationException::class, fn () => $client->emitentes->consultar())->mensagem);
    lanca(AlmztechAuthorizationException::class, fn () => $client->emitentes->consultar());
    igual(422, lanca(AlmztechValidationException::class, fn () => $client->emitentes->consultar())->statusCode);
}

function teste429ComRetryAfterSemRetentar(): void
{
    $envio = (new EnvioFalso())->responder(429, '{"mensagem":"Limite de requisições excedido."}', ['retry-after' => '60']);
    $erro = lanca(AlmztechRateLimitException::class, fn () => client($envio)->notasFiscais->consultarProtocolo('HML-1'));
    igual(60, $erro->retryAfter);
    igual(1, count($envio->chamadas));
}

function teste400DoAspNetViraValidacaoComOsCampos(): void
{
    $envio = (new EnvioFalso())->responder(400, '{"title":"x","errors":{"notas[0].servicos[0].retemISS":["Could not convert."]}}');
    $erro = lanca(AlmztechValidationException::class, fn () => client($envio)->notasFiscais->emitir(LOTE_VAZIO));
    igual(400, $erro->statusCode);
    verificar(str_contains($erro->mensagem, 'retemISS'), $erro->mensagem);
}

// ---------------- polling ----------------

function testePollingDevolveNoSucesso(): void
{
    $envio = (new EnvioFalso())->responder(200, fixture('protocolo-responses/em-fila.json'))->responder(200, fixture('protocolo-responses/emissao-sucesso.json'));
    $resultado = client($envio)->notasFiscais->aguardarConclusao('SBOX-1', intervaloInicial: 0.001, intervaloMaximo: 0.002);
    igual('PROCESSADO_COM_SUCESSO', $resultado['status']);
    verificar(!empty($resultado['notas']));
}

function testePollingRejeicaoLanca(): void
{
    $envio = (new EnvioFalso())->responder(200, fixture('protocolo-responses/rejeitado.json'));
    $erro = lanca(AlmztechProtocoloRejeitadoException::class, fn () => client($envio)->notasFiscais->aguardarConclusao('SBOX-1', intervaloInicial: 0.001));
    igual('REJEITADO', $erro->protocolo['status']);
}

function testePollingEstouraOTempo(): void
{
    $envio = new EnvioFalso();
    for ($i = 0; $i < 50; $i++) {
        $envio->responder(200, EM_FILA);
    }
    $erro = lanca(AlmztechPollingTimeoutException::class,
        fn () => client($envio)->notasFiscais->aguardarConclusao('HML-1', intervaloInicial: 0.001, intervaloMaximo: 0.002, timeoutTotal: 0.005));
    igual('EM_FILA', $erro->ultimoProtocolo['status']);
}

// ---------------- webhook e log ----------------

function testeWebhookClassificaOsTresEventos(): void
{
    igual(Webhook::EMISSAO, Webhook::ler(fixture('webhook-payloads/emissao.json'))['tipo']);
    igual(Webhook::CANCELAMENTO, Webhook::ler(fixture('webhook-payloads/cancelamento.json'))['tipo']);
    igual(Webhook::REJEICAO, Webhook::ler(fixture('webhook-payloads/rejeicao.json'))['tipo']);
    igual(Webhook::EMISSAO, Webhook::ler(json_decode(fixture('webhook-payloads/emissao.json'), true))['tipo']);
}

function testeWebhookRecusaVazioInvalidoEDesconhecido(): void
{
    foreach (['', '{nao json', '{"protocolo":"X","status":"EM_FILA"}'] as $corpo) {
        lanca(AlmztechWebhookParsingException::class, fn () => Webhook::ler($corpo));
    }
}

function testeLogMascaraEmQualquerProfundidade(): void
{
    $mascarado = LogSanitizer::sanitizarCorpoJson('{"cpfCnpj":"123","senhaCertificado":"s1","endereco":{"senhaPrefeitura":"s2"},"lista":[{"base64":"AAA"}],"arquivoBase64":"UEs="}');
    foreach (['s1', 's2', 'AAA', 'UEs='] as $segredo) {
        verificar(!str_contains($mascarado, $segredo), $segredo);
    }
    verificar(str_contains($mascarado, '123'));
    igual('<corpo não-JSON omitido da sanitização de log>', LogSanitizer::sanitizarCorpoJson('não é json'));
    igual('***1234', LogSanitizer::sanitizarApiKey('abcdefgh1234'));
}

function testeLoggerNuncaRecebeChaveNemSenha(): void
{
    $linhas = [];
    $envio = (new EnvioFalso())->responder(200, '[{"razaoSocial":"X","cpfCnpj":"1","senhaCertificado":"segredo"}]');
    client($envio, function (string $linha) use (&$linhas) { $linhas[] = $linha; }, 'chave-secreta-9999')->emitentes->consultar();
    $tudo = implode("\n", $linhas);
    verificar(!str_contains($tudo, 'chave-secreta') && !str_contains($tudo, 'segredo'), $tudo);
    verificar(str_contains($tudo, '***9999'), $tudo);
}

// ---------------- contrato ----------------

function testeEmitirMandaCodigosNumericosOmiteNulosEHeaders(): void
{
    $envio = (new EnvioFalso())->responder(200, EM_FILA);
    client($envio)->notasFiscais->emitir([
        'identificacaoEmitente' => '12345678000190',
        'enviarParaProducao' => false,
        'notas' => [[
            'rps' => ['serie' => '1', 'tipo' => '1', 'dataEmissao' => '2026-10-31', 'numeroRPS' => null],
            'destinatario' => ['cpfCnpj' => '12345678909', 'razaoSocial' => 'Cliente', 'endereco' => ['codigoIBGEMunicipio' => 2611606]],
            'servicos' => [['codigo' => '0105', 'descricao' => 'Teste', 'valorUnitario' => 100.0, 'retemISS' => 2]],
            'observacao' => null,
        ]],
    ]);
    $nota = $envio->corpo()['notas'][0];
    igual(2, $nota['servicos'][0]['retemISS']);
    verificar(!array_key_exists('numeroRPS', $nota['rps']), 'nulo enviado');
    verificar(!array_key_exists('observacao', $nota), 'nulo enviado');
    verificar(str_contains($envio->chamadas[0]['corpo'], '"valorUnitario":100.0'), $envio->chamadas[0]['corpo']);
    igual('api-key-de-teste', $envio->chamadas[0]['headers']['x-api-key']);
    verificar(str_starts_with($envio->chamadas[0]['headers']['User-Agent'], 'almztech-php-sdk/'));
}

function testeMunicipiosIdNumericoEQuery(): void
{
    $envio = (new EnvioFalso())->responder(200, '[{"id":2611606,"text":"2611606 - Recife"}]');
    igual(2611606, client($envio)->municipios->homologados(2611606)[0]['id']);
    igual('/CoberturaNacional/MunicipiosHomologados?codigo=2611606', $envio->chamadas[0]['caminho']);
}

function testeCadastroDevolveCodigoEUploadMandaBase64(): void
{
    $envio = (new EnvioFalso())
        ->responder(200, '{"mensagem":"Emitente cadastrado com sucesso.","codigo":42}')
        ->responder(200, '{"mensagem":"ok","chaveCertificado":"abc.pfx"}');
    $client = client($envio);
    igual(42, $client->emitentes->cadastrar(['razaoSocial' => 'Empresa', 'cpfCnpj' => '1'])['codigo']);
    $stream = fopen('php://memory', 'r+');
    fwrite($stream, "\x01\x02\x03");
    rewind($stream);
    igual('abc.pfx', $client->emitentes->uploadCertificado('1', $stream, 'senha')['chaveCertificado']);
    igual('AQID', $envio->corpo(1)['base64']);
}

function testeImportarPlanilhaMandaBase64(): void
{
    $envio = (new EnvioFalso())->responder(200, '{"protocolos":[{"protocolo":"HML-9","status":"EM_FILA"}],"totalLinhasLidas":3,"totalNotasValidas":2,"erros":[{"linha":4,"mensagem":"CPF inválido"}]}');
    $resultado = client($envio)->notasFiscais->importarPlanilha('1', "\x01\x02\x03", false);
    igual(4, $resultado['erros'][0]['linha']);
    igual('AQID', $envio->corpo()['arquivoBase64']);
    verificar(!array_key_exists('urlWebhook', $envio->corpo()), 'nulo enviado');
}

function testeFiltrosComoTexto(): void
{
    $envio = (new EnvioFalso())->responder(200, '{"notas":[],"paginaAtual":1,"totalDeRegistros":0,"contagensPorAba":{"all":5}}');
    $resultado = client($envio)->notasFiscais->consultarComFiltros(Filtros::PRODUCAO, [
        Filtros::periodo('2026-10-01', new \DateTimeImmutable('2026-10-31')),
        Filtros::comStatus('EMITIDA', 'CANCELADA'),
        Filtros::entreValores(Filtros::CAMPO_VALOR_TOTAL, 10.5, 100),
    ]);
    igual(5, $resultado['contagensPorAba']['all']);
    $corpo = $envio->corpo();
    igual('PRD', $corpo['ambiente']);
    verificar(!array_key_exists('filtroRapido', $corpo), 'nulo enviado');
    igual(['campo' => 'dataEmissao', 'operador' => 'Entre', 'valor' => '2026-10-01', 'valorFinal' => '2026-10-31'], $corpo['filtros'][0]);
    igual('EMITIDA|CANCELADA', $corpo['filtros'][1]['valor']);
    igual('10.5', $corpo['filtros'][2]['valor']);
    igual('100', $corpo['filtros'][2]['valorFinal']);
}

function testePaginasPercorreAteOTotal(): void
{
    $envio = (new EnvioFalso())
        ->responder(200, '{"notas":[{"codigoNota":1},{"codigoNota":2}],"paginaAtual":1,"totalDeRegistros":3}')
        ->responder(200, '{"notas":[{"codigoNota":3}],"paginaAtual":2,"totalDeRegistros":3}');
    $codigos = [];
    foreach (client($envio)->notasFiscais->paginas(Filtros::PRODUCAO, limiteDaPagina: 2) as $nota) {
        $codigos[] = $nota['codigoNota'];
    }
    igual([1, 2, 3], $codigos);
    igual(2, $envio->corpo(1)['pagina']);
}

function testeReconsultaPorChaveEWebhookComEventoNumerico(): void
{
    $envio = (new EnvioFalso())
        ->responder(200, '{"codigoNota":7,"situacao":"CANCELADA","protocolo":"PRD-1"}')
        ->responder(200, '{"codigo":"g","evento":0,"ativo":true,"url":"https://x","codigoEmitente":1}');
    $client = client($envio);
    igual('CANCELADA', $client->notasFiscais->reconsultarPorChaveAcesso('NFS26116', true)['situacao']);
    igual(['enviarParaProducao' => true], $envio->corpo(0));
    $client->webhooks->criar(1, Webhooks::EMISSAO, 'https://x');
    igual(0, $envio->corpo(1)['evento']);
}

function testeListaDoFixture(): void
{
    $envio = (new EnvioFalso())->responder(200, fixture('nota-fiscal-resumida/lista-notas.json'));
    $notas = client($envio)->notasFiscais->consultarLista(Filtros::PRODUCAO);
    verificar($notas !== [] && is_int($notas[0]['codigoNota']));
}

// ---------------- runner ----------------

$falhas = 0;
$total = 0;
foreach (get_defined_functions()['user'] as $funcao) {
    $reflexao = new \ReflectionFunction($funcao);
    if (!str_starts_with($reflexao->getShortName(), 'teste')) {
        continue;
    }
    $total++;
    try {
        $reflexao->invoke();
        echo "ok    {$reflexao->getShortName()}\n";
    } catch (\Throwable $erro) {
        $falhas++;
        echo "FALHA {$reflexao->getShortName()}: {$erro->getMessage()}\n";
    }
}
echo "{$total} testes, " . ($total - $falhas) . " passaram, {$falhas} falharam\n";
exit($falhas > 0 ? 1 : 0);
