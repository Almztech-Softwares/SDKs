# @almztech/sdk (Node.js/TypeScript)

SDK oficial da [Almztech](https://almztech.com.br) para Node.js e TypeScript. Ele cobre a API de NFS-e:
- emissão, consulta, reconsulta e cancelamento de notas, inclusive por planilha;
- emitentes e certificados;
- webhooks;
- municípios homologados.

> Versão beta: a API pública do SDK ainda pode mudar até a 1.0.

## Instalação

```bash
npm install @almztech/sdk@beta
```

- Node 18+, ESM, tipos inclusos.
- **Sem dependências de runtime**: usa o `fetch` nativo (dá para injetar outro em `opcoes.fetch`).
- Os dados são objetos no formato exato do JSON da API, em camelCase, tipados por interfaces (`RecepcaoLote`, `ProtocoloResponse`...). Propriedades `undefined`/`null` não são enviadas.

## Uso

```ts
import { AlmztechClient, AlmztechErroProtocoloRejeitado, AlmztechErroValidacao, lerWebhook } from "@almztech/sdk";

// A api key está no Portal (portal.almztech.com.br), em Primeiros passos. Guarde-a na configuração do seu
// sistema, nunca no código.
const client = new AlmztechClient(process.env.ALMZTECH_API_KEY!);

try {
	const protocolo = await client.notasFiscais.emitir({
		identificacaoEmitente: "12345678000190",
		enviarParaProducao: false, // homologação (HML-...): não consome a franquia
		notas: [
			{
				rps: { numeroRPS: "1001", serie: "1", tipo: "1", dataEmissao: "2026-10-31" },
				destinatario: {
					cpfCnpj: "11222333000181",
					razaoSocial: "Cliente Exemplo Ltda",
					endereco: { cep: "50030170", codigoIBGEMunicipio: 2611606 },
				},
				servicos: [{ codigo: "0105", descricao: "Licenciamento de software", quantidade: 1, valorUnitario: 1500, aliquotaISS: 2, retemISS: 2 }],
			},
		],
	});

	const resultado = await client.notasFiscais.aguardarConclusao(protocolo.protocolo);
	for (const nota of resultado.notas ?? []) {
		const pdf = await client.notasFiscais.baixarPdf(nota.codigoNota);
		// gravar o PDF fica com você (ex.: fs.writeFile)
	}
} catch (erro) {
	if (erro instanceof AlmztechErroProtocoloRejeitado) console.error("Rejeitada:", erro.protocolo.mensagem);
	else if (erro instanceof AlmztechErroValidacao) console.error("Recusada:", erro.mensagem);
	else throw erro;
}

// No seu endpoint de webhook (a API não assina o payload: proteja a URL):
const evento = lerWebhook(corpoRecebido); // { tipo: "Emissao" | "Cancelamento" | "Rejeicao", protocolo }
```

Consulta com filtros e paginação automática:

```ts
import { Ambiente, Filtros } from "@almztech/sdk";

for await (const nota of client.notasFiscais.paginas({
	ambiente: Ambiente.producao,
	filtros: [Filtros.periodo("2026-10-01", "2026-10-31"), Filtros.comStatus("EMITIDA")],
}))
	console.log(nota.numeroNota, nota.valorTotal);
```

Também estão disponíveis:
- **Notas:** `consultarPorNumero`, `reconsultar`, `reconsultarPorChaveAcesso`, `cancelar`, `baixarXml`, `baixarModeloPlanilha` e `importarPlanilha` (aceita caminho, bytes ou stream).
- **Emitentes:** `client.emitentes`, inclusive `uploadCertificado`.
- **Webhooks e municípios:** `client.webhooks` e `client.municipios`.

Exceções:
- `AlmztechErroValidacao` (422 e 400);
- `AlmztechErroAutenticacao` (401);
- `AlmztechErroAutorizacao` (403);
- `AlmztechErroLimiteDeRequisicoes` (429, com `retryAfterMs`);
- `AlmztechErroServidor` (5xx);
- `AlmztechErroRede`;
- `AlmztechErroTempoDePollingEsgotado`;
- `AlmztechErroProtocoloRejeitado`;
- `AlmztechErroWebhook`.

Só as leituras são retentadas automaticamente. `emitir`, `cancelar` e `importarPlanilha` nunca são.

## Links

- [Documentação da API](https://docs.almztech.com.br)
- [Código-fonte](https://github.com/Almztech-Softwares/SDKs/tree/main/nodejs). Os SDKs das outras linguagens (.NET, PHP,
  Java, Python e Ruby) seguem o mesmo [contrato](https://github.com/Almztech-Softwares/SDKs/blob/main/DESIGN.md).

## Licença

MIT, © 2026 ALMZTECH SOFTWARES LTDA. Veja [LICENSE](https://github.com/Almztech-Softwares/SDKs/blob/main/nodejs/LICENSE).
