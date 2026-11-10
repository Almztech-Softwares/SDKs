# almztech-sdk (Java)

SDK oficial da [Almztech](https://almztech.com.br) para Java 11+. Ele cobre a API de NFS-e:
- emissão, consulta, reconsulta e cancelamento de notas, inclusive por planilha;
- emitentes e certificados;
- webhooks;
- municípios homologados.

> Versão beta: a API pública do SDK ainda pode mudar até a 1.0.

## Instalação

```xml
<dependency>
  <groupId>br.com.almztech</groupId>
  <artifactId>almztech-sdk</artifactId>
  <version>0.2.0-beta.1</version>
</dependency>
```

- Java 11+.
- **Sem dependências de runtime**: usa `java.net.http` e um JSON próprio, para não impor Jackson/Gson, nem conflitos de versão, ao ERP que integra. Dá para injetar outro envio HTTP em `AlmztechClientOptions.envioHttp`.
- Os modelos (`com.almztech.sdk.modelos`) têm campos públicos com os mesmos nomes do JSON. Campos nulos não são enviados.
- As exceções (`com.almztech.sdk.erros`) são unchecked.

## Uso

```java
// A api key está no Portal (portal.almztech.com.br), em Primeiros passos. Guarde-a na configuração do seu
// sistema, nunca no código. Crie um client por aplicação e reaproveite: ele é thread-safe.
AlmztechClient client = new AlmztechClient(System.getenv("ALMZTECH_API_KEY"));

RecepcaoLote lote = new RecepcaoLote();
lote.identificacaoEmitente = "12345678000190";
lote.enviarParaProducao = false; // homologação (HML-...): não consome a franquia

NotaFiscalResumida nota = new NotaFiscalResumida();
nota.rps = new IdentificacaoDaNota();
nota.rps.numeroRPS = "1001";
nota.rps.serie = "1";
nota.rps.tipo = "1";
nota.rps.dataEmissao = LocalDate.now();
nota.destinatario = new Destinatario();
nota.destinatario.cpfCnpj = "11222333000181";
nota.destinatario.razaoSocial = "Cliente Exemplo Ltda";
nota.destinatario.endereco = new Endereco();
nota.destinatario.endereco.codigoIBGEMunicipio = 2611606;
Servico servico = new Servico();
servico.codigo = "0105";
servico.descricao = "Licenciamento de software";
servico.valorUnitario = new BigDecimal("1500.00");
servico.aliquotaISS = new BigDecimal("2");
servico.retemISS = 2;
nota.servicos = List.of(servico);
lote.notas = List.of(nota);

try {
    ProtocoloResponse protocolo = client.notasFiscais().emitir(lote);
    ProtocoloResponse resultado = client.notasFiscais().aguardarConclusao(protocolo.protocolo);
    for (ProtocoloNotaResponse emitida : resultado.notas)
        Files.write(Path.of("nfse-" + emitida.numero + ".pdf"), client.notasFiscais().baixarPdf(emitida.codigoNota));
} catch (AlmztechProtocoloRejeitadoException e) {
    System.out.println("Rejeitada: " + e.getProtocolo().mensagem);
} catch (AlmztechValidationException e) {
    System.out.println("Recusada: " + e.getMensagem());
}

// No seu endpoint de webhook (a API não assina o payload: proteja a URL):
AlmztechWebhook.Evento evento = AlmztechWebhook.ler(corpoRecebido); // evento.tipo: EMISSAO, CANCELAMENTO ou REJEICAO
```

Consulta com filtros e paginação automática:

```java
ParametrosFiltros parametros = new ParametrosFiltros(); // ambiente: Ambiente.PRODUCAO por padrão
parametros.filtros = List.of(Filtros.periodo(LocalDate.of(2026, 10, 1), LocalDate.of(2026, 10, 31)), Filtros.comStatus(StatusNota.EMITIDA));
for (NotaFiscal nota : client.notasFiscais().paginas(parametros))
    System.out.println(nota.numeroNota + " " + nota.valorTotal);
```

Exceções:
- `AlmztechValidationException` (422 e 400);
- `AlmztechAuthenticationException` (401);
- `AlmztechAuthorizationException` (403);
- `AlmztechRateLimitException` (429, com `getRetryAfter()`);
- `AlmztechServerException` (5xx);
- `AlmztechNetworkException`;
- `AlmztechPollingTimeoutException`;
- `AlmztechProtocoloRejeitadoException`;
- `AlmztechWebhookParsingException`.

Só as leituras são retentadas automaticamente. `emitir`, `cancelar` e `importarPlanilha` nunca são. Para ler `.pfx`/`.xlsx` de arquivo ou stream, use `Arquivos.ler(...)`.

## Links

- [Documentação da API](https://docs.almztech.com.br)
- [Código-fonte](https://github.com/Almztech-Softwares/SDKs/tree/main/java). Os SDKs das outras linguagens (.NET, Node.js,
  PHP, Python e Ruby) seguem o mesmo [contrato](https://github.com/Almztech-Softwares/SDKs/blob/main/DESIGN.md).

## Licença

MIT, © 2026 ALMZTECH SOFTWARES LTDA. Veja [LICENSE](https://github.com/Almztech-Softwares/SDKs/blob/main/java/LICENSE).
