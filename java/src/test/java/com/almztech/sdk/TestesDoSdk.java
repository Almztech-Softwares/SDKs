package com.almztech.sdk;

import com.almztech.sdk.erros.AlmztechAuthenticationException;
import com.almztech.sdk.erros.AlmztechAuthorizationException;
import com.almztech.sdk.erros.AlmztechNetworkException;
import com.almztech.sdk.erros.AlmztechPollingTimeoutException;
import com.almztech.sdk.erros.AlmztechProtocoloRejeitadoException;
import com.almztech.sdk.erros.AlmztechRateLimitException;
import com.almztech.sdk.erros.AlmztechServerException;
import com.almztech.sdk.erros.AlmztechValidationException;
import com.almztech.sdk.erros.AlmztechWebhookParsingException;
import com.almztech.sdk.json.Json;
import com.almztech.sdk.modelos.Ambiente;
import com.almztech.sdk.modelos.CamposFiltro;
import com.almztech.sdk.modelos.Destinatario;
import com.almztech.sdk.modelos.Emitente;
import com.almztech.sdk.modelos.Endereco;
import com.almztech.sdk.modelos.Filtros;
import com.almztech.sdk.modelos.IdentificacaoDaNota;
import com.almztech.sdk.modelos.MunicipioHomologado;
import com.almztech.sdk.modelos.NotaFiscal;
import com.almztech.sdk.modelos.NotaFiscalResumida;
import com.almztech.sdk.modelos.ParametrosFiltros;
import com.almztech.sdk.modelos.ProtocoloResponse;
import com.almztech.sdk.modelos.RecepcaoLote;
import com.almztech.sdk.modelos.ResultadoConsultaComFiltros;
import com.almztech.sdk.modelos.ResultadoImportacaoPlanilha;
import com.almztech.sdk.modelos.Servico;
import com.almztech.sdk.modelos.StatusNota;
import com.almztech.sdk.modelos.StatusProtocolo;
import com.almztech.sdk.modelos.Webhook;
import com.almztech.sdk.modelos.WebhookEvento;
import java.io.IOException;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import java.math.BigDecimal;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.time.Duration;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.LinkedList;
import java.util.List;
import java.util.Map;

/**
 * Testes do SDK Java contra os fixtures compartilhados (fixtures/) e o contrato atual da API. Runner próprio,
 * sem JUnit, para rodar só com o JDK: todo método estático público {@code void teste*()} é um teste.
 * Rode a partir de {@code java/}: {@code java -cp <classes> com.almztech.sdk.TestesDoSdk}.
 */
public final class TestesDoSdk {
	private static final String EM_FILA = "{\"protocolo\":\"HML-1\",\"status\":\"EM_FILA\"}";

	public static void main(String[] args) throws Exception {
		int falhas = 0;
		int total = 0;
		for (Method metodo : TestesDoSdk.class.getDeclaredMethods()) {
			if (!metodo.getName().startsWith("teste") || !Modifier.isStatic(metodo.getModifiers()))
				continue;
			total++;
			try {
				metodo.invoke(null);
				System.out.println("ok    " + metodo.getName());
			} catch (InvocationTargetException erro) {
				falhas++;
				System.out.println("FALHA " + metodo.getName() + ": " + erro.getCause());
			}
		}
		System.out.println(total + " testes, " + (total - falhas) + " passaram, " + falhas + " falharam");
		if (falhas > 0)
			System.exit(1);
	}

	// ---------------- infraestrutura ----------------

	static final class EnvioFalso implements EnvioHttp {
		final LinkedList<Object> fila = new LinkedList<>();
		final List<String[]> chamadas = new ArrayList<>(); // método, caminho, corpo
		final List<Map<String, String>> headers = new ArrayList<>();

		EnvioFalso responder(int status, String corpo) {
			return responder(status, corpo, Map.of());
		}

		EnvioFalso responder(int status, String corpo, Map<String, String> headers) {
			Map<String, List<String>> lista = new java.util.HashMap<>();
			headers.forEach((nome, valor) -> lista.put(nome, List.of(valor)));
			fila.add(new Resposta(status, lista, corpo.getBytes(StandardCharsets.UTF_8)));
			return this;
		}

		EnvioFalso falharRede() {
			fila.add(new IOException("rede indisponível"));
			return this;
		}

		@Override
		public Resposta enviar(String metodo, String url, Map<String, String> headers, byte[] corpo, Duration timeout) throws IOException {
			chamadas.add(new String[] { metodo, url.replace("https://sdk-tests.local", ""), corpo == null ? null : new String(corpo, StandardCharsets.UTF_8) });
			this.headers.add(headers);
			Object proxima = fila.removeFirst();
			if (proxima instanceof IOException)
				throw (IOException) proxima;
			return (Resposta) proxima;
		}

		@SuppressWarnings("unchecked")
		Map<String, Object> corpo(int indice) {
			return (Map<String, Object>) Json.ler(chamadas.get(indice)[2]);
		}
	}

	static AlmztechClient client(EnvioFalso envio) {
		return new AlmztechClient("api-key-de-teste", new AlmztechClientOptions().baseUrl("https://sdk-tests.local").envioHttp(envio)
				.retry(new RetryOptions().tentativas(3).intervaloInicial(Duration.ofMillis(1)).intervaloMaximo(Duration.ofMillis(5))));
	}

	/** Payload do fixture como a API o enviaria (sem {@code _comentario}; erros guardam em {@code body}, listas em {@code response}). */
	@SuppressWarnings("unchecked")
	static String fixture(String caminho) throws IOException {
		Path arquivo = Paths.get("..", "fixtures", caminho);
		Map<String, Object> json = (Map<String, Object>) Json.ler(new String(Files.readAllBytes(arquivo), StandardCharsets.UTF_8));
		json.remove("_comentario");
		Object payload = json.containsKey("body") ? json.get("body") : json.containsKey("response") ? json.get("response") : json;
		return Json.escrever(payload);
	}

	static void verificar(boolean condicao, String mensagem) {
		if (!condicao)
			throw new AssertionError(mensagem);
	}

	static void igual(Object esperado, Object atual) {
		if (esperado == null ? atual != null : !esperado.equals(atual))
			throw new AssertionError("esperado <" + esperado + "> mas veio <" + atual + ">");
	}

	interface Acao {
		void executar() throws Exception;
	}

	@SuppressWarnings("unchecked")
	static <T extends Throwable> T lanca(Class<T> tipo, Acao acao) {
		try {
			acao.executar();
		} catch (Throwable erro) {
			if (tipo.isInstance(erro))
				return (T) erro;
			throw new AssertionError("esperado " + tipo.getSimpleName() + " mas veio " + erro, erro);
		}
		throw new AssertionError("esperado " + tipo.getSimpleName() + ", nada foi lançado");
	}

	static RecepcaoLote loteVazio() {
		RecepcaoLote lote = new RecepcaoLote();
		lote.identificacaoEmitente = "1";
		lote.notas = new ArrayList<>();
		return lote;
	}

	// ---------------- retry ----------------

	public static void testeLeituraRetentaEm5xx() {
		EnvioFalso envio = new EnvioFalso().responder(500, "").responder(503, "").responder(200, EM_FILA);
		igual(StatusProtocolo.EM_FILA, client(envio).notasFiscais().consultarProtocolo("HML-1").status);
		igual(3, envio.chamadas.size());
	}

	public static void testeLeituraDesisteAposFalhasDeRede() {
		EnvioFalso envio = new EnvioFalso().falharRede().falharRede().falharRede();
		lanca(AlmztechNetworkException.class, () -> client(envio).notasFiscais().consultarProtocolo("HML-1"));
		igual(3, envio.chamadas.size());
	}

	public static void testeEmitirCancelarEImportarNuncaRetentam() {
		EnvioFalso envio = new EnvioFalso().responder(500, "").falharRede().responder(500, "").responder(500, "");
		AlmztechClient client = client(envio);
		lanca(AlmztechServerException.class, () -> client.notasFiscais().emitir(loteVazio()));
		lanca(AlmztechNetworkException.class, () -> client.notasFiscais().emitir(loteVazio()));
		lanca(AlmztechServerException.class, () -> client.notasFiscais().cancelar(7));
		lanca(AlmztechServerException.class, () -> client.notasFiscais().importarPlanilha("1", new byte[] { 1 }, false, null));
		igual(4, envio.chamadas.size());
	}

	public static void teste4xxNaoRetenta() throws IOException {
		EnvioFalso envio = new EnvioFalso().responder(422, fixture("erros/422-validacao.json"));
		lanca(AlmztechValidationException.class, () -> client(envio).notasFiscais().consultarPorCodigo(999999));
		igual(1, envio.chamadas.size());
	}

	// ---------------- erros ----------------

	public static void testeErrosTipadosComAMensagem() throws IOException {
		EnvioFalso envio = new EnvioFalso().responder(401, fixture("erros/401-sem-token.json"))
				.responder(403, fixture("erros/403-proibido.json")).responder(422, fixture("erros/422-validacao.json"));
		AlmztechClient client = client(envio);
		igual("Token não informado", lanca(AlmztechAuthenticationException.class, () -> client.emitentes().consultar()).getMensagem());
		lanca(AlmztechAuthorizationException.class, () -> client.emitentes().consultar());
		igual(422, lanca(AlmztechValidationException.class, () -> client.emitentes().consultar()).getStatusCode());
	}

	public static void teste429ComRetryAfterSemRetentar() {
		EnvioFalso envio = new EnvioFalso().responder(429, "{\"mensagem\":\"Limite de requisições excedido.\"}", Map.of("Retry-After", "60"));
		AlmztechRateLimitException erro = lanca(AlmztechRateLimitException.class, () -> client(envio).notasFiscais().consultarProtocolo("HML-1"));
		igual(Duration.ofSeconds(60), erro.getRetryAfter());
		igual(1, envio.chamadas.size());
	}

	public static void teste400DoAspNetViraValidacaoComOsCampos() {
		EnvioFalso envio = new EnvioFalso().responder(400, "{\"title\":\"x\",\"errors\":{\"notas[0].servicos[0].retemISS\":[\"Could not convert.\"]}}");
		AlmztechValidationException erro = lanca(AlmztechValidationException.class, () -> client(envio).notasFiscais().emitir(loteVazio()));
		igual(400, erro.getStatusCode());
		verificar(erro.getMensagem().contains("retemISS"), erro.getMensagem());
	}

	// ---------------- polling ----------------

	static AguardarConclusaoOptions esperaRapida() {
		AguardarConclusaoOptions opcoes = new AguardarConclusaoOptions();
		opcoes.intervaloInicial = Duration.ofMillis(1);
		opcoes.intervaloMaximo = Duration.ofMillis(2);
		return opcoes;
	}

	public static void testePollingDevolveNoSucesso() throws IOException {
		EnvioFalso envio = new EnvioFalso().responder(200, fixture("protocolo-responses/em-fila.json"))
				.responder(200, fixture("protocolo-responses/emissao-sucesso.json"));
		ProtocoloResponse resultado = client(envio).notasFiscais().aguardarConclusao("SBOX-1", esperaRapida());
		igual(StatusProtocolo.PROCESSADO_COM_SUCESSO, resultado.status);
		verificar(!resultado.notas.isEmpty(), "sem notas");
		verificar(resultado.notas.get(0).dataEmissao != null, "dataEmissao não lida");
	}

	public static void testePollingRejeicaoLanca() throws IOException {
		EnvioFalso envio = new EnvioFalso().responder(200, fixture("protocolo-responses/rejeitado.json"));
		AlmztechProtocoloRejeitadoException erro = lanca(AlmztechProtocoloRejeitadoException.class,
				() -> client(envio).notasFiscais().aguardarConclusao("SBOX-1", esperaRapida()));
		igual(StatusProtocolo.REJEITADO, erro.getProtocolo().status);
	}

	public static void testePollingEstouraOTempo() {
		EnvioFalso envio = new EnvioFalso();
		for (int i = 0; i < 50; i++)
			envio.responder(200, EM_FILA);
		AguardarConclusaoOptions opcoes = esperaRapida();
		opcoes.timeoutTotal = Duration.ofMillis(5);
		AlmztechPollingTimeoutException erro = lanca(AlmztechPollingTimeoutException.class,
				() -> client(envio).notasFiscais().aguardarConclusao("HML-1", opcoes));
		igual(StatusProtocolo.EM_FILA, erro.getUltimoProtocolo().status);
	}

	// ---------------- webhook e log ----------------

	public static void testeWebhookClassificaOsTresEventos() throws IOException {
		igual(AlmztechWebhook.TipoEvento.EMISSAO, AlmztechWebhook.ler(fixture("webhook-payloads/emissao.json")).tipo);
		igual(AlmztechWebhook.TipoEvento.CANCELAMENTO, AlmztechWebhook.ler(fixture("webhook-payloads/cancelamento.json")).tipo);
		igual(AlmztechWebhook.TipoEvento.REJEICAO, AlmztechWebhook.ler(fixture("webhook-payloads/rejeicao.json")).tipo);
	}

	public static void testeWebhookRecusaVazioInvalidoEDesconhecido() {
		for (String corpo : new String[] { "", "{nao json", "{\"protocolo\":\"X\",\"status\":\"EM_FILA\"}" })
			lanca(AlmztechWebhookParsingException.class, () -> AlmztechWebhook.ler(corpo));
	}

	public static void testeLogMascaraEmQualquerProfundidade() {
		String mascarado = LogSanitizer.sanitizarCorpoJson(
				"{\"cpfCnpj\":\"123\",\"senhaCertificado\":\"s1\",\"endereco\":{\"senhaPrefeitura\":\"s2\"},\"lista\":[{\"base64\":\"AAA\"}],\"arquivoBase64\":\"UEs=\"}");
		for (String segredo : new String[] { "s1", "s2", "AAA", "UEs=" })
			verificar(!mascarado.contains(segredo), segredo);
		verificar(mascarado.contains("123"), mascarado);
		igual("<corpo não-JSON omitido da sanitização de log>", LogSanitizer.sanitizarCorpoJson("não é json"));
		igual("***1234", LogSanitizer.sanitizarApiKey("abcdefgh1234"));
	}

	public static void testeLoggerNuncaRecebeChaveNemSenha() {
		List<String> linhas = new ArrayList<>();
		EnvioFalso envio = new EnvioFalso().responder(200, "[{\"razaoSocial\":\"X\",\"cpfCnpj\":\"1\",\"senhaCertificado\":\"segredo\"}]");
		new AlmztechClient("chave-secreta-9999", new AlmztechClientOptions().baseUrl("https://sdk-tests.local").envioHttp(envio).logger(linhas::add))
				.emitentes().consultar();
		String tudo = String.join("\n", linhas);
		verificar(!tudo.contains("chave-secreta") && !tudo.contains("segredo"), tudo);
		verificar(tudo.contains("***9999"), tudo);
	}

	// ---------------- contrato ----------------

	@SuppressWarnings("unchecked")
	public static void testeEmitirMandaCodigosNumericosOmiteNulosEHeaders() {
		EnvioFalso envio = new EnvioFalso().responder(200, EM_FILA);
		RecepcaoLote lote = loteVazio();
		NotaFiscalResumida nota = new NotaFiscalResumida();
		nota.rps = new IdentificacaoDaNota();
		nota.rps.serie = "1";
		nota.rps.tipo = "1";
		nota.rps.dataEmissao = LocalDate.of(2026, 10, 31);
		nota.destinatario = new Destinatario();
		nota.destinatario.cpfCnpj = "12345678909";
		nota.destinatario.razaoSocial = "Cliente";
		nota.destinatario.endereco = new Endereco();
		nota.destinatario.endereco.codigoIBGEMunicipio = 2611606;
		Servico servico = new Servico();
		servico.codigo = "0105";
		servico.descricao = "Teste";
		servico.valorUnitario = new BigDecimal("100.00");
		servico.retemISS = 2;
		nota.servicos = List.of(servico);
		lote.notas = List.of(nota);
		client(envio).notasFiscais().emitir(lote);

		Map<String, Object> enviada = ((List<Map<String, Object>>) envio.corpo(0).get("notas")).get(0);
		Map<String, Object> rps = (Map<String, Object>) enviada.get("rps");
		Map<String, Object> item = ((List<Map<String, Object>>) enviada.get("servicos")).get(0);
		igual(new BigDecimal("2"), item.get("retemISS"));
		igual("2026-10-31", rps.get("dataEmissao"));
		verificar(!rps.containsKey("numeroRPS"), "nulo enviado");
		verificar(!enviada.containsKey("observacao"), "nulo enviado");
		igual("100.00", ((BigDecimal) item.get("valorUnitario")).toPlainString());
		igual("api-key-de-teste", envio.headers.get(0).get("x-api-key"));
		verificar(envio.headers.get(0).get("User-Agent").startsWith("almztech-java-sdk/"), "User-Agent");
	}

	public static void testeMunicipiosIdNumericoEQuery() {
		EnvioFalso envio = new EnvioFalso().responder(200, "[{\"id\":2611606,\"text\":\"2611606 - Recife\"}]");
		List<MunicipioHomologado> municipios = client(envio).municipios().homologados(2611606, null);
		igual(2611606, municipios.get(0).id);
		igual("/CoberturaNacional/MunicipiosHomologados?codigo=2611606", envio.chamadas.get(0)[1]);
	}

	public static void testeCadastroDevolveCodigoEUploadMandaBase64() {
		EnvioFalso envio = new EnvioFalso().responder(200, "{\"mensagem\":\"Emitente cadastrado com sucesso.\",\"codigo\":42}")
				.responder(200, "{\"mensagem\":\"ok\",\"chaveCertificado\":\"abc.pfx\",\"vencimento\":\"2027-05-01T00:00:00\"}");
		AlmztechClient client = client(envio);
		Emitente emitente = new Emitente();
		emitente.razaoSocial = "Empresa";
		emitente.cpfCnpj = "1";
		igual(42, client.emitentes().cadastrar(emitente).codigo);
		igual("abc.pfx", client.emitentes().uploadCertificado("1", new byte[] { 1, 2, 3 }, "senha").chaveCertificado);
		igual("AQID", envio.corpo(1).get("base64"));
	}

	public static void testeImportarPlanilhaMandaBase64() {
		EnvioFalso envio = new EnvioFalso().responder(200,
				"{\"protocolos\":[{\"protocolo\":\"HML-9\",\"status\":\"EM_FILA\"}],\"totalLinhasLidas\":3,\"totalNotasValidas\":2,\"erros\":[{\"linha\":4,\"mensagem\":\"CPF inválido\"}]}");
		ResultadoImportacaoPlanilha resultado = client(envio).notasFiscais().importarPlanilha("1", new byte[] { 1, 2, 3 }, false, null);
		igual(4, resultado.erros.get(0).linha);
		igual(StatusProtocolo.EM_FILA, resultado.protocolos.get(0).status);
		igual("AQID", envio.corpo(0).get("arquivoBase64"));
		verificar(!envio.corpo(0).containsKey("urlWebhook"), "nulo enviado");
	}

	@SuppressWarnings("unchecked")
	public static void testeFiltrosComoTextoEContagens() {
		EnvioFalso envio = new EnvioFalso().responder(200,
				"{\"notas\":[{\"codigoNota\":7,\"numeroNota\":\"123\",\"status\":\"EMITIDA\",\"protocolo\":\"PRD-1\",\"valorTotal\":10.5}],\"paginaAtual\":1,\"totalDeRegistros\":1,\"contagensPorAba\":{\"all\":5,\"CANCELADA\":4}}");
		ParametrosFiltros parametros = new ParametrosFiltros();
		parametros.filtros = List.of(Filtros.periodo(LocalDate.of(2026, 10, 1), LocalDate.of(2026, 10, 31)),
				Filtros.comStatus(StatusNota.EMITIDA, StatusNota.CANCELADA),
				Filtros.entreValores(CamposFiltro.VALOR_TOTAL, new BigDecimal("10.5"), new BigDecimal("100")));
		ResultadoConsultaComFiltros resultado = client(envio).notasFiscais().consultarComFiltros(parametros);

		igual(4, resultado.contagensPorAba.get("CANCELADA"));
		igual(StatusNota.EMITIDA, resultado.notas.get(0).status);
		igual(new BigDecimal("10.5"), resultado.notas.get(0).valorTotal);
		Map<String, Object> corpo = envio.corpo(0);
		igual(Ambiente.PRODUCAO, corpo.get("ambiente"));
		List<Map<String, Object>> filtros = (List<Map<String, Object>>) corpo.get("filtros");
		igual("Entre", filtros.get(0).get("operador"));
		igual("2026-10-01", filtros.get(0).get("valor"));
		igual("2026-10-31", filtros.get(0).get("valorFinal"));
		igual("EMITIDA|CANCELADA", filtros.get(1).get("valor"));
		igual("10.5", filtros.get(2).get("valor"));
	}

	public static void testePaginasPercorreAteOTotal() {
		EnvioFalso envio = new EnvioFalso()
				.responder(200, "{\"notas\":[{\"codigoNota\":1},{\"codigoNota\":2}],\"paginaAtual\":1,\"totalDeRegistros\":3}")
				.responder(200, "{\"notas\":[{\"codigoNota\":3}],\"paginaAtual\":2,\"totalDeRegistros\":3}");
		ParametrosFiltros parametros = new ParametrosFiltros();
		parametros.limiteDaPagina = 2;
		List<Integer> codigos = new ArrayList<>();
		for (NotaFiscal nota : client(envio).notasFiscais().paginas(parametros))
			codigos.add(nota.codigoNota);
		igual(List.of(1, 2, 3), codigos);
		igual(new BigDecimal("2"), envio.corpo(1).get("pagina"));
	}

	public static void testeReconsultaPorChaveEWebhookComEventoNumerico() {
		EnvioFalso envio = new EnvioFalso().responder(200, "{\"codigoNota\":7,\"situacao\":\"CANCELADA\",\"protocolo\":\"PRD-1\"}")
				.responder(200, "{\"codigo\":\"g\",\"evento\":0,\"ativo\":true,\"url\":\"https://x\",\"codigoEmitente\":1}");
		AlmztechClient client = client(envio);
		igual(StatusNota.CANCELADA, client.notasFiscais().reconsultarPorChaveAcesso("NFS26116", true).situacao);
		igual(Boolean.TRUE, envio.corpo(0).get("enviarParaProducao"));
		Webhook webhook = new Webhook();
		webhook.codigoEmitente = 1;
		webhook.evento = WebhookEvento.EMISSAO;
		webhook.url = "https://x";
		client.webhooks().criar(webhook);
		igual(BigDecimal.ZERO, envio.corpo(1).get("evento"));
		igual(Boolean.TRUE, envio.corpo(1).get("ativo"));
	}

	public static void testeListaDoFixture() throws IOException {
		EnvioFalso envio = new EnvioFalso().responder(200, fixture("nota-fiscal-resumida/lista-notas.json"));
		List<NotaFiscal> notas = client(envio).notasFiscais().consultarLista(Ambiente.PRODUCAO);
		verificar(!notas.isEmpty(), "lista vazia");
		verificar(notas.get(0).codigoNota > 0, "codigoNota não lido");
	}
}
