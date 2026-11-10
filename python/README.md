# almztech-sdk (Python)

SDK oficial da [Almztech](https://almztech.com.br) para Python. Ele cobre a API de NFS-e:
- emissão, consulta, reconsulta e cancelamento de notas, inclusive por planilha;
- emitentes e certificados;
- webhooks;
- municípios homologados.

> Versão beta: a API pública do SDK ainda pode mudar até a 1.0.

## Instalação

```bash
pip install --pre almztech-sdk
```

- Python 3.9+.
- **Sem dependências de runtime**: usa `urllib` e `json` da biblioteca padrão (dá para injetar outro envio HTTP em `OpcoesDoClient.envio_http`).
- Os dados são `dict` no formato exato do JSON da API, em camelCase, com `TypedDict` em `almztech.modelos` para o editor. Chaves com `None` não são enviadas.

## Uso

```python
import os
from almztech import AlmztechClient, AlmztechErroProtocoloRejeitado, AlmztechErroValidacao, filtros, ler_webhook

# A api key está no Portal (portal.almztech.com.br), em Primeiros passos. Guarde-a na configuração do seu
# sistema, nunca no código.
client = AlmztechClient(os.environ["ALMZTECH_API_KEY"])

try:
    protocolo = client.notas_fiscais.emitir({
        "identificacaoEmitente": "12345678000190",
        "enviarParaProducao": False,  # homologação (HML-...): não consome a franquia
        "notas": [{
            "rps": {"numeroRPS": "1001", "serie": "1", "tipo": "1", "dataEmissao": "2026-10-31"},
            "destinatario": {
                "cpfCnpj": "11222333000181",
                "razaoSocial": "Cliente Exemplo Ltda",
                "endereco": {"cep": "50030170", "codigoIBGEMunicipio": 2611606},
            },
            "servicos": [{"codigo": "0105", "descricao": "Licenciamento de software", "quantidade": 1,
                          "valorUnitario": 1500, "aliquotaISS": 2, "retemISS": 2}],
        }],
    })

    resultado = client.notas_fiscais.aguardar_conclusao(protocolo["protocolo"])
    for nota in resultado.get("notas", []):
        pdf = client.notas_fiscais.baixar_pdf(nota["codigoNota"])  # bytes
except AlmztechErroProtocoloRejeitado as erro:
    print("Rejeitada:", erro.protocolo.get("mensagem"))
except AlmztechErroValidacao as erro:
    print("Recusada:", erro.mensagem)

# No seu endpoint de webhook (a API não assina o payload: proteja a URL):
evento = ler_webhook(corpo_recebido)  # evento.tipo: "Emissao" | "Cancelamento" | "Rejeicao"
```

Consulta com filtros e paginação automática:

```python
for nota in client.notas_fiscais.paginas(
    filtros.PRODUCAO,
    [filtros.periodo("2026-10-01", "2026-10-31"), filtros.com_status("EMITIDA")],
):
    print(nota["numeroNota"], nota["valorTotal"])
```

Também estão disponíveis:
- **Notas:** `consultar_por_numero`, `reconsultar`, `reconsultar_por_chave_acesso`, `cancelar`, `baixar_xml`, `baixar_modelo_planilha` e `importar_planilha` (aceita caminho, bytes ou arquivo aberto).
- **Emitentes:** `client.emitentes`, inclusive `upload_certificado`.
- **Webhooks e municípios:** `client.webhooks` e `client.municipios`.

Exceções:
- `AlmztechErroValidacao` (422 e 400);
- `AlmztechErroAutenticacao` (401);
- `AlmztechErroAutorizacao` (403);
- `AlmztechErroLimiteDeRequisicoes` (429, com `retry_after`);
- `AlmztechErroServidor` (5xx);
- `AlmztechErroRede`;
- `AlmztechErroTempoDePollingEsgotado`;
- `AlmztechErroProtocoloRejeitado`;
- `AlmztechErroWebhook`.

Só as leituras são retentadas automaticamente. `emitir`, `cancelar` e `importar_planilha` nunca são.

## Links

- [Documentação da API](https://docs.almztech.com.br)
- [Código-fonte](https://github.com/Almztech-Softwares/SDKs/tree/main/python). Os SDKs das outras linguagens (.NET, Node.js,
  PHP, Java e Ruby) seguem o mesmo [contrato](https://github.com/Almztech-Softwares/SDKs/blob/main/DESIGN.md).

## Licença

MIT, © 2026 ALMZTECH SOFTWARES LTDA. Veja [LICENSE](https://github.com/Almztech-Softwares/SDKs/blob/main/python/LICENSE).
