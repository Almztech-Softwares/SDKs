import assert from "node:assert/strict";
import { describe, it } from "node:test";
import {
	AlmztechClient,
	AlmztechErroAutenticacao,
	AlmztechErroAutorizacao,
	AlmztechErroLimiteDeRequisicoes,
	AlmztechErroProtocoloRejeitado,
	AlmztechErroRede,
	AlmztechErroServidor,
	AlmztechErroTempoDePollingEsgotado,
	AlmztechErroValidacao,
	AlmztechErroWebhook,
	Ambiente,
	CamposFiltro,
	Filtros,
	lerWebhook,
	mascararApiKey,
	mascararCorpoJson,
	WebhookEvento,
} from "../src/index.js";
import { criarClient, FetchFalso, fixture } from "./auxiliares.js";

const emFila = JSON.stringify({ protocolo: "HML-1", status: "EM_FILA" });

describe("retry (DESIGN.md seção 4)", () => {
	it("leitura retenta em 5xx e devolve o sucesso", async () => {
		const falso = new FetchFalso().responder(500).responder(503).responder(200, emFila);
		const resposta = await criarClient(falso).notasFiscais.consultarProtocolo("HML-1");
		assert.equal(resposta.status, "EM_FILA");
		assert.equal(falso.chamadas.length, 3);
	});

	it("leitura retenta em falha de rede e desiste após as tentativas", async () => {
		const falso = new FetchFalso().falharRede().falharRede().falharRede();
		await assert.rejects(criarClient(falso).notasFiscais.consultarProtocolo("HML-1"), AlmztechErroRede);
		assert.equal(falso.chamadas.length, 3);
	});

	it("emitir nunca é retentado, nem em 5xx nem em falha de rede", async () => {
		const falso = new FetchFalso().responder(500, JSON.stringify({ mensagem: "falhou" }));
		await assert.rejects(criarClient(falso).notasFiscais.emitir({ identificacaoEmitente: "1", notas: [], enviarParaProducao: false }), AlmztechErroServidor);
		assert.equal(falso.chamadas.length, 1);

		const rede = new FetchFalso().falharRede();
		await assert.rejects(criarClient(rede).notasFiscais.emitir({ identificacaoEmitente: "1", notas: [], enviarParaProducao: false }), AlmztechErroRede);
		assert.equal(rede.chamadas.length, 1);
	});

	it("cancelar e importar planilha não são retentados", async () => {
		const falso = new FetchFalso().responder(500).responder(500);
		const client = criarClient(falso);
		await assert.rejects(client.notasFiscais.cancelar(7), AlmztechErroServidor);
		await assert.rejects(client.notasFiscais.importarPlanilha("1", new Uint8Array([1]), false), AlmztechErroServidor);
		assert.equal(falso.chamadas.length, 2);
	});

	it("4xx não é retentado", async () => {
		const falso = new FetchFalso().responder(422, fixture("erros/422-validacao.json"));
		await assert.rejects(criarClient(falso).notasFiscais.consultarPorCodigo(999999), AlmztechErroValidacao);
		assert.equal(falso.chamadas.length, 1);
	});
});

describe("mapeamento de erros", () => {
	it("401, 403 e 422 viram exceções tipadas com a mensagem da API", async () => {
		const falso = new FetchFalso()
			.responder(401, fixture("erros/401-sem-token.json"))
			.responder(403, fixture("erros/403-proibido.json"))
			.responder(422, fixture("erros/422-validacao.json"));
		const client = criarClient(falso);

		await assert.rejects(client.emitentes.consultar(), (erro: AlmztechErroAutenticacao) => erro instanceof AlmztechErroAutenticacao && erro.mensagem === "Token não informado");
		await assert.rejects(client.emitentes.consultar(), AlmztechErroAutorizacao);
		await assert.rejects(client.emitentes.consultar(), (erro: AlmztechErroValidacao) => erro.statusCode === 422 && erro.mensagem.length > 0);
	});

	it("429 traz o Retry-After e não é retentado", async () => {
		const falso = new FetchFalso().responder(429, JSON.stringify({ mensagem: "Limite de requisições excedido." }), { "Retry-After": "60" });
		await assert.rejects(criarClient(falso).notasFiscais.consultarProtocolo("HML-1"), (erro: AlmztechErroLimiteDeRequisicoes) => {
			assert.ok(erro instanceof AlmztechErroLimiteDeRequisicoes);
			assert.equal(erro.retryAfterMs, 60_000);
			return true;
		});
		assert.equal(falso.chamadas.length, 1);
	});

	it("400 do ASP.NET vira validação com os erros por campo", async () => {
		const problema = { title: "One or more validation errors occurred.", status: 400, errors: { "notas[0].servicos[0].retemISS": ["Could not convert string to integer: true."] } };
		const falso = new FetchFalso().responder(400, JSON.stringify(problema));
		await assert.rejects(criarClient(falso).notasFiscais.emitir({ identificacaoEmitente: "1", notas: [], enviarParaProducao: false }), (erro: AlmztechErroValidacao) => {
			assert.equal(erro.statusCode, 400);
			assert.match(erro.mensagem, /retemISS/);
			return true;
		});
	});
});

describe("aguardarConclusao", () => {
	const opcoes = { intervaloInicialMs: 1, intervaloMaximoMs: 2 };

	it("devolve no status terminal de sucesso", async () => {
		const falso = new FetchFalso().responder(200, fixture("protocolo-responses/em-fila.json")).responder(200, fixture("protocolo-responses/emissao-sucesso.json"));
		const resultado = await criarClient(falso).notasFiscais.aguardarConclusao("SBOX-1", opcoes);
		assert.equal(resultado.status, "PROCESSADO_COM_SUCESSO");
		assert.ok(resultado.notas!.length > 0);
	});

	it("rejeição lança com o protocolo e a mensagem", async () => {
		const falso = new FetchFalso().responder(200, fixture("protocolo-responses/rejeitado.json"));
		await assert.rejects(criarClient(falso).notasFiscais.aguardarConclusao("SBOX-1", opcoes), (erro: AlmztechErroProtocoloRejeitado) => {
			assert.equal(erro.protocolo.status, "REJEITADO");
			return true;
		});
	});

	it("estoura o tempo com o último protocolo conhecido", async () => {
		const falso = new FetchFalso();
		for (let i = 0; i < 50; i++) falso.responder(200, emFila);
		await assert.rejects(criarClient(falso).notasFiscais.aguardarConclusao("HML-1", { ...opcoes, timeoutTotalMs: 5 }), (erro: AlmztechErroTempoDePollingEsgotado) => {
			assert.equal(erro.ultimoProtocolo?.status, "EM_FILA");
			return true;
		});
	});
});

describe("webhook", () => {
	it("classifica emissão, cancelamento e rejeição pelos fixtures", () => {
		assert.equal(lerWebhook(fixture("webhook-payloads/emissao.json")).tipo, "Emissao");
		assert.equal(lerWebhook(fixture("webhook-payloads/cancelamento.json")).tipo, "Cancelamento");
		assert.equal(lerWebhook(fixture("webhook-payloads/rejeicao.json")).tipo, "Rejeicao");
		assert.equal(lerWebhook(JSON.parse(fixture("webhook-payloads/emissao.json"))).tipo, "Emissao");
	});

	it("recusa corpo vazio, inválido ou não reconhecido", () => {
		assert.throws(() => lerWebhook(""), AlmztechErroWebhook);
		assert.throws(() => lerWebhook("{nao json"), AlmztechErroWebhook);
		assert.throws(() => lerWebhook(JSON.stringify({ protocolo: "X", status: "EM_FILA" })), AlmztechErroWebhook);
	});
});

describe("mascaramento de log", () => {
	it("mascara senhas, certificado e planilha em qualquer profundidade", () => {
		const corpo = JSON.stringify({ cpfCnpj: "123", senhaCertificado: "s1", endereco: { senhaPrefeitura: "s2" }, lista: [{ base64: "AAA" }], arquivoBase64: "UEs=" });
		const mascarado = mascararCorpoJson(corpo);
		for (const segredo of ["s1", "s2", "AAA", "UEs="]) assert.ok(!mascarado.includes(segredo), segredo);
		assert.ok(mascarado.includes("123"));
		assert.equal(mascararCorpoJson("não é json"), "<corpo não-JSON omitido da sanitização de log>");
		assert.equal(mascararApiKey("abcdefgh1234"), "***1234");
	});

	it("o logger do client nunca recebe a api key nem senhas", async () => {
		const linhas: string[] = [];
		const falso = new FetchFalso().responder(200, JSON.stringify([{ razaoSocial: "X", cpfCnpj: "1", senhaCertificado: "segredo" }]));
		const client = new AlmztechClient("chave-secreta-9999", { baseUrl: "https://sdk-tests.local", fetch: falso.fetch, logger: (l) => linhas.push(l) });
		await client.emitentes.consultar();
		const tudo = linhas.join("\n");
		assert.ok(!tudo.includes("chave-secreta") && !tudo.includes("segredo"));
		assert.ok(tudo.includes("***9999"));
	});
});

describe("contrato com a API atual", () => {
	it("emitir manda códigos fiscais como número, omite nulos e manda os headers", async () => {
		const falso = new FetchFalso().responder(200, emFila);
		await criarClient(falso).notasFiscais.emitir({
			identificacaoEmitente: "12345678000190",
			enviarParaProducao: false,
			notas: [
				{
					rps: { serie: "1", tipo: "1", dataEmissao: "2026-10-31", numeroRPS: undefined },
					destinatario: { cpfCnpj: "12345678909", razaoSocial: "Cliente", endereco: { codigoIBGEMunicipio: 2611606 } },
					servicos: [{ codigo: "0105", descricao: "Teste", valorUnitario: 100, retemISS: 2, exigibilidadeISS: 1, aliquotaISS: 2.5 }],
					observacao: null as unknown as string,
				},
			],
		});

		const nota = falso.corpoJson().notas[0];
		assert.equal(nota.servicos[0].retemISS, 2);
		assert.ok(!("numeroRPS" in nota.rps));
		assert.ok(!("observacao" in nota), "null é omitido");
		assert.equal(falso.chamadas[0].headers["x-api-key"], "api-key-de-teste");
		assert.match(falso.chamadas[0].headers["User-Agent"], /^almztech-nodejs-sdk\//);
	});

	it("municípios lê o id numérico e manda o código IBGE na query", async () => {
		const falso = new FetchFalso().responder(200, JSON.stringify([{ id: 2611606, text: "2611606 - Recife" }]));
		const municipios = await criarClient(falso).municipios.homologados({ codigoIbge: 2611606 });
		assert.equal(municipios[0].id, 2611606);
		assert.equal(falso.chamadas[0].caminho, "/CoberturaNacional/MunicipiosHomologados?codigo=2611606");
	});

	it("cadastrar emitente devolve o código e upload manda Base64", async () => {
		const falso = new FetchFalso()
			.responder(200, JSON.stringify({ mensagem: "Emitente cadastrado com sucesso.", cpfCnpj: "1", codigo: 42 }))
			.responder(200, JSON.stringify({ mensagem: "ok", chaveCertificado: "abc.pfx", vencimento: "2027-05-01T00:00:00" }));
		const client = criarClient(falso);

		assert.equal((await client.emitentes.cadastrar({ razaoSocial: "Empresa", cpfCnpj: "1" })).codigo, 42);
		const upload = await client.emitentes.uploadCertificado("1", new Uint8Array([1, 2, 3]), "senha");
		assert.equal(upload.chaveCertificado, "abc.pfx");
		assert.equal(falso.corpoJson(1).base64, "AQID");
	});

	it("importar planilha manda o arquivo em Base64 e lê protocolos e erros", async () => {
		const resultado = { protocolos: [{ protocolo: "HML-9", status: "EM_FILA" }], totalLinhasLidas: 3, totalNotasValidas: 2, erros: [{ linha: 4, mensagem: "CPF inválido" }] };
		const falso = new FetchFalso().responder(200, JSON.stringify(resultado));
		const importado = await criarClient(falso).notasFiscais.importarPlanilha("1", new Uint8Array([1, 2, 3]), false);
		assert.equal(importado.erros[0].linha, 4);
		assert.equal(falso.corpoJson().arquivoBase64, "AQID");
	});

	it("filtros mandam valores como texto e lêem as contagens por aba", async () => {
		const resposta = { notas: [{ codigoNota: 7, numeroNota: "123", status: "EMITIDA", protocolo: "PRD-1" }], paginaAtual: 1, totalDeRegistros: 1, contagensPorAba: { all: 5, CANCELADA: 4 } };
		const falso = new FetchFalso().responder(200, JSON.stringify(resposta));
		const resultado = await criarClient(falso).notasFiscais.consultarComFiltros({
			ambiente: Ambiente.producao,
			filtros: [Filtros.periodo("2026-10-01", "2026-10-31"), Filtros.comStatus("EMITIDA", "CANCELADA"), Filtros.entreValores(CamposFiltro.valorTotal, 10.5, 100)],
		});

		assert.equal(resultado.contagensPorAba.CANCELADA, 4);
		const corpo = falso.corpoJson();
		assert.equal(corpo.ambiente, "PRD");
		assert.deepEqual(corpo.filtros[0], { campo: "dataEmissao", operador: "Entre", valor: "2026-10-01", valorFinal: "2026-10-31" });
		assert.equal(corpo.filtros[1].valor, "EMITIDA|CANCELADA");
		assert.equal(corpo.filtros[2].valor, "10.5");
	});

	it("o iterador percorre as páginas até o total", async () => {
		const pagina = (notas: number[], total: number) => JSON.stringify({ notas: notas.map((codigoNota) => ({ codigoNota })), paginaAtual: 1, totalDeRegistros: total, contagensPorAba: {} });
		const falso = new FetchFalso().responder(200, pagina([1, 2], 3)).responder(200, pagina([3], 3));
		const codigos: number[] = [];
		for await (const nota of criarClient(falso).notasFiscais.paginas({ ambiente: Ambiente.producao, limiteDaPagina: 2 })) codigos.push(nota.codigoNota);
		assert.deepEqual(codigos, [1, 2, 3]);
		assert.equal(falso.corpoJson(1).pagina, 2);
	});

	it("reconsultar por chave manda o ambiente no corpo; webhook manda o evento como número", async () => {
		const falso = new FetchFalso()
			.responder(200, JSON.stringify({ codigoNota: 7, situacao: "CANCELADA", dataEmissao: "2026-10-01", protocolo: "PRD-1" }))
			.responder(200, JSON.stringify({ codigo: "g", evento: 0, ativo: true, url: "https://x", codigoEmitente: 1 }));
		const client = criarClient(falso);

		assert.equal((await client.notasFiscais.reconsultarPorChaveAcesso("NFS26116", true)).situacao, "CANCELADA");
		assert.deepEqual(falso.corpoJson(0), { enviarParaProducao: true });
		await client.webhooks.criar({ evento: WebhookEvento.Emissao, url: "https://x", codigoEmitente: 1 });
		assert.equal(falso.corpoJson(1).evento, 0);
	});

	it("lista de notas do fixture tem os campos tipados", async () => {
		const falso = new FetchFalso().responder(200, fixture("nota-fiscal-resumida/lista-notas.json"));
		const notas = await criarClient(falso).notasFiscais.consultarLista(Ambiente.producao);
		assert.ok(notas.length > 0);
		assert.equal(typeof notas[0].codigoNota, "number");
	});
});
