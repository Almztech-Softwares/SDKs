# Almztech.Sdk (.NET)

SDK oficial da [Almztech](https://almztech.com.br) para .NET 8. Ele cobre a API de NFS-e:
- emissão, consulta e cancelamento de notas;
- emitentes e certificados;
- webhooks;
- municípios homologados.

Sem dependências além do próprio .NET.

> Versão beta: a API pública do SDK ainda pode mudar até a 1.0.

## Instalação

```bash
dotnet add package Almztech.Sdk --prerelease
```

## Autenticação

O SDK usa a **api key** da sua conta, que fica no [Portal](https://portal.almztech.com.br), em Primeiros passos. Guarde-a na configuração do seu sistema (variável de ambiente, cofre de segredos), nunca no código.

```csharp
using Almztech.Sdk;

using var client = new AlmztechClient(Environment.GetEnvironmentVariable("ALMZTECH_API_KEY")!);
```

Crie um `AlmztechClient` por aplicação e reaproveite: ele é thread-safe.

Produção e homologação usam a mesma URL e a mesma chave. O ambiente é escolhido a cada emissão
(`EnviarParaProducao`).

## Emitir uma nota

```csharp
using Almztech.Sdk.Models;

var lote = new RecepcaoLote
{
    IdentificacaoEmitente = "12345678000190",
    EnviarParaProducao = false, // homologação
    Notas =
    [
        new NotaFiscalResumida
        {
            Rps = new IdentificacaoDaNota { NumeroRPS = "1", Serie = "1", Tipo = "1" },
            Destinatario = new Destinatario
            {
                CpfCnpj = "12345678909",
                RazaoSocial = "Cliente Teste",
                Endereco = new Endereco { CodigoIBGEMunicipio = 2611606, Cep = "50030230" },
            },
            Servicos = [ new Servico { Codigo = "0105", Descricao = "Serviço de teste", ValorUnitario = 100m, AliquotaISS = 2m, RetemISS = 2 } ],
        },
    ],
};

var protocolo = await client.NotasFiscais.EmitirAsync(lote);
```

A emissão é assíncrona: a API devolve um protocolo, e a prefeitura processa o lote em seguida. Há duas formas de
acompanhar:
- **Webhook** (recomendado): veja a seção abaixo.
- **Polling**: `AguardarConclusaoAsync` consulta o protocolo com backoff até a conclusão. Se o lote for rejeitado
  ou terminar com erro, ele lança `AlmztechProtocoloRejeitadoException`.

```csharp
var resultado = await client.NotasFiscais.AguardarConclusaoAsync(protocolo.Protocolo);
var pdf = await client.NotasFiscais.BaixarPdfAsync(resultado.Notas[0].CodigoNota);
```

`EmitirAsync` e `CancelarAsync` **nunca** são repetidos automaticamente: a API não tem chave de idempotência, e
repetir poderia emitir a nota duas vezes. Leituras são repetidas em falha de rede ou erro 5xx (`RetryOptions`).

## Webhooks

Cadastre a URL do seu sistema com `client.Webhooks.CriarAsync`, por emitente e evento. Teste a URL antes com
`TestarAsync`. No seu endpoint, leia o evento recebido:

```csharp
using Almztech.Sdk.Webhooks;

app.MapPost("/webhooks/almztech", async (HttpRequest req) =>
{
    using var leitor = new StreamReader(req.Body);
    var evento = AlmztechWebhook.Parse(await leitor.ReadToEndAsync());

    switch (evento.Tipo)
    {
        case TipoEventoWebhook.Emissao: /* evento.Notas */ break;
        case TipoEventoWebhook.Cancelamento: /* evento.DadosCancelamento */ break;
        case TipoEventoWebhook.Rejeicao: /* evento.Mensagem */ break;
    }

    return Results.Ok(); // só HTTP 200 conta como entregue
});
```

A API só considera a notificação entregue quando recebe **HTTP 200** em até 10 segundos. Fora disso, ela tenta
de novo, até 3 vezes.

## Consultar notas

Use `ConsultarComFiltrosPaginasAsync` para filtros com paginação automática:

```csharp
var filtros = new DtoParametrosFiltros
{
    Ambiente = Ambiente.Producao,
    Filtros = [ DtoFiltro.Periodo(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), DtoFiltro.ComStatus(StatusNota.EMITIDA) ],
};

await foreach (var nota in client.NotasFiscais.ConsultarComFiltrosPaginasAsync(filtros))
    Console.WriteLine($"{nota.NumeroNota} {nota.ValorTotal}");
```

Também existem consultas por protocolo, código, número da nota e termo de busca. Para gerar as notas a partir de
uma planilha, use `BaixarModeloPlanilhaAsync` e depois `ImportarPlanilhaAsync`.

## O que mais o client oferece

| Recurso | Operações |
|---|---|
| `client.NotasFiscais` | emitir, aguardar conclusão, consultar, reconsultar na prefeitura, cancelar, baixar XML/PDF, importar planilha |
| `client.Emitentes` | consultar, cadastrar, atualizar, excluir, enviar certificado digital |
| `client.Webhooks` | listar, criar, atualizar, excluir, testar |
| `client.Municipios` | municípios homologados |

## Configuração

```csharp
using var client = new AlmztechClient(apiKey, new AlmztechClientOptions
{
    Timeout = TimeSpan.FromSeconds(60),
    RetryOptions = new RetryOptions { MaxAttempts = 5 },
    Logger = mensagem => logger.LogDebug("{Mensagem}", mensagem),
});
```

- **`Logger`**: recebe as requisições e respostas já mascaradas. Senhas, login e certificado saem como `***`, e a
  api key aparece só com os 4 últimos caracteres.
- **`HttpClient`**: aceita um cliente próprio, para proxy ou instrumentação.

## Erros

Todas as exceções derivam de `AlmztechException`, em `Almztech.Sdk.Exceptions`:

| Exceção | Quando |
|---|---|
| `AlmztechValidationException` | 400 e 422, com os erros por campo |
| `AlmztechAuthenticationException` | 401: api key inválida ou revogada |
| `AlmztechAuthorizationException` | 403 |
| `AlmztechRateLimitException` | 429, com `RetryAfter` |
| `AlmztechServerException` | 5xx |
| `AlmztechNetworkException` | falha de rede ou timeout |
| `AlmztechPollingTimeoutException` | `AguardarConclusaoAsync` passou do tempo máximo |
| `AlmztechProtocoloRejeitadoException` | o lote foi rejeitado ou terminou com erro |
| `AlmztechWebhookParsingException` | corpo de webhook que não é um evento válido |

## Mudanças da 0.2 (quebram compatibilidade com a 0.1)

- **Emissão**: os modelos seguem o schema atual da API.
  - Códigos fiscais do `Servico` são `int?`.
  - `Destinatario` não tem mais e-mail e ganhou nome fantasia e inscrições.
  - `Pagamento` tem `Parcela` e `DataPagamento`.
  - IBS/CBS, PIS/COFINS e totais dos tributos passaram a ser tipados.
  - Campos nulos não são mais enviados.
- **Emitentes**: `CadastrarAsync`/`AtualizarAsync` devolvem `ResultadoGravacaoEmitente`, e `UploadCertificadoAsync`
  devolve `ResultadoUploadCertificado`.
- **Filtros**: `DtoFiltro.Valor` virou string, com construtores (`Periodo`, `ComStatus`, `EntreValores`, `Data`,
  `Numero`) e `CamposFiltro`.
- **Métodos novos**:
  - `ConsultarPorNumeroAsync`;
  - `ReconsultarAsync` e `ReconsultarPorChaveAcessoAsync`;
  - `BaixarModeloPlanilhaAsync` e `ImportarPlanilhaAsync`.
- **Erros**: `AlmztechRateLimitException` é nova, e o 400 de validação agora vem como
  `AlmztechValidationException`.

## Links

- [Documentação da API](https://docs.almztech.com.br)
- [Código-fonte](https://github.com/Almztech-Softwares/SDKs/tree/main/dotnet). Os SDKs das outras linguagens (Node.js,
  PHP, Java, Python e Ruby) seguem o mesmo [contrato](https://github.com/Almztech-Softwares/SDKs/blob/main/DESIGN.md).

## Licença

MIT, © 2026 ALMZTECH SOFTWARES LTDA. Veja [LICENSE](https://github.com/Almztech-Softwares/SDKs/blob/main/dotnet/LICENSE).
