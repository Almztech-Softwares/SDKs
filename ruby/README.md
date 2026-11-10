# almztech-sdk (Ruby)

SDK oficial da [Almztech](https://almztech.com.br) para Ruby. Ele cobre a API de NFS-e:
- emissão, consulta, reconsulta e cancelamento de notas, inclusive por planilha;
- emitentes e certificados;
- webhooks;
- municípios homologados.

> Versão beta: a API pública do SDK ainda pode mudar até a 1.0.

## Instalação

```bash
gem install almztech-sdk --pre
```

- Ruby 3.0+.
- **Sem dependências de runtime**: usa `net/http` e `json` da biblioteca padrão. Dá para injetar outro envio HTTP com `envio_http:`, qualquer objeto que responda a `call`.
- Os dados são `Hash` no formato exato do JSON da API, em camelCase. Na entrada aceita chaves símbolo ou string; as respostas vêm com chaves string. Valores `nil` não são enviados.

## Uso

```ruby
require "almztech"

# A api key está no Portal (portal.almztech.com.br), em Primeiros passos. Guarde-a na configuração do seu
# sistema, nunca no código.
client = Almztech::Client.new(ENV.fetch("ALMZTECH_API_KEY"))

begin
  protocolo = client.notas_fiscais.emitir(
    identificacaoEmitente: "12345678000190",
    enviarParaProducao: false, # homologação (HML-...): não consome a franquia
    notas: [{
      rps: { numeroRPS: "1001", serie: "1", tipo: "1", dataEmissao: Date.today.iso8601 },
      destinatario: { cpfCnpj: "11222333000181", razaoSocial: "Cliente Exemplo Ltda",
                      endereco: { cep: "50030170", codigoIBGEMunicipio: 2_611_606 } },
      servicos: [{ codigo: "0105", descricao: "Licenciamento de software", quantidade: 1,
                   valorUnitario: 1500.0, aliquotaISS: 2, retemISS: 2 }]
    }]
  )

  resultado = client.notas_fiscais.aguardar_conclusao(protocolo["protocolo"])
  resultado.fetch("notas", []).each do |nota|
    File.binwrite("nfse-#{nota['numero']}.pdf", client.notas_fiscais.baixar_pdf(nota["codigoNota"]))
  end
rescue Almztech::ErroProtocoloRejeitado => e
  puts "Rejeitada: #{e.protocolo['mensagem']}"
rescue Almztech::ErroValidacao => e
  puts "Recusada: #{e.mensagem}"
end

# No seu endpoint de webhook (a API não assina o payload: proteja a URL):
evento = Almztech::Webhook.ler(request.body.read) # evento.tipo: :emissao, :cancelamento ou :rejeicao
```

Consulta com filtros e paginação automática:

```ruby
filtros = [Almztech::Filtros.periodo("2026-10-01", "2026-10-31"), Almztech::Filtros.com_status("EMITIDA")]
client.notas_fiscais.paginas(Almztech::Filtros::PRODUCAO, filtros: filtros).each do |nota|
  puts "#{nota['numeroNota']} #{nota['valorTotal']}"
end
```

Exceções:
- `Almztech::ErroValidacao` (422 e 400);
- `ErroAutenticacao` (401);
- `ErroAutorizacao` (403);
- `ErroLimiteDeRequisicoes` (429, com `retry_after`);
- `ErroServidor` (5xx);
- `ErroRede`;
- `ErroTempoDePollingEsgotado`;
- `ErroProtocoloRejeitado`;
- `ErroWebhook`.

Só as leituras são retentadas automaticamente. `emitir`, `cancelar` e `importar_planilha` nunca são.

## Links

- [Documentação da API](https://docs.almztech.com.br)
- [Código-fonte](https://github.com/Almztech-Softwares/SDKs/tree/main/ruby). Os SDKs das outras linguagens (.NET, Node.js,
  PHP, Java e Python) seguem o mesmo [contrato](https://github.com/Almztech-Softwares/SDKs/blob/main/DESIGN.md).

## Licença

MIT, © 2026 ALMZTECH SOFTWARES LTDA. Veja [LICENSE](https://github.com/Almztech-Softwares/SDKs/blob/main/ruby/LICENSE).
