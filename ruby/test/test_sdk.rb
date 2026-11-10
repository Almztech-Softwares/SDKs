require "minitest/autorun"
require "json"
require "stringio"
require_relative "../lib/almztech"

# Testes do SDK Ruby contra os fixtures compartilhados (fixtures/) e o contrato atual da API.
# Rode a partir de ruby/: ruby -Ilib test/test_sdk.rb
module Auxiliares
  FIXTURES = File.expand_path("../../fixtures", __dir__)
  EM_FILA = JSON.generate("protocolo" => "HML-1", "status" => "EM_FILA")
  LOTE_VAZIO = { "identificacaoEmitente" => "1", "notas" => [], "enviarParaProducao" => false }.freeze

  # Envio HTTP falso: responde em fila e guarda método, caminho, headers e corpo de cada chamada.
  class EnvioFalso
    attr_reader :chamadas

    def initialize
      @fila = []
      @chamadas = []
    end

    def responder(status, corpo = "", headers = {})
      @fila << [status, headers, corpo]
      self
    end

    def falhar_rede
      @fila << SocketError.new("rede indisponível")
      self
    end

    def call(metodo, url, headers, corpo, _timeout)
      @chamadas << { metodo: metodo, caminho: url.sub("https://sdk-tests.local", ""), headers: headers, corpo: corpo }
      resposta = @fila.shift
      raise resposta if resposta.is_a?(Exception)

      resposta
    end

    def corpo(indice = 0) = JSON.parse(@chamadas[indice][:corpo])
  end

  # Payload do fixture como a API o enviaria (sem _comentario; erros guardam em body, listas em response).
  def fixture(caminho)
    dados = JSON.parse(File.read(File.join(FIXTURES, caminho)))
    dados.delete("_comentario")
    JSON.generate(dados["body"] || dados["response"] || dados)
  end

  def client(envio, logger: nil, api_key: "api-key-de-teste")
    Almztech::Client.new(api_key, base_url: "https://sdk-tests.local", envio_http: envio, logger: logger,
                                  intervalo_inicial: 0.001, intervalo_maximo: 0.005)
  end
end

class RetryTeste < Minitest::Test
  include Auxiliares

  def test_leitura_retenta_em_5xx
    envio = EnvioFalso.new.responder(500).responder(503).responder(200, EM_FILA)
    assert_equal "EM_FILA", client(envio).notas_fiscais.consultar_protocolo("HML-1")["status"]
    assert_equal 3, envio.chamadas.size
  end

  def test_leitura_desiste_apos_falhas_de_rede
    envio = EnvioFalso.new.falhar_rede.falhar_rede.falhar_rede
    assert_raises(Almztech::ErroRede) { client(envio).notas_fiscais.consultar_protocolo("HML-1") }
    assert_equal 3, envio.chamadas.size
  end

  def test_emitir_cancelar_e_importar_nunca_retentam
    envio = EnvioFalso.new.responder(500).falhar_rede.responder(500).responder(500)
    sdk = client(envio)
    assert_raises(Almztech::ErroServidor) { sdk.notas_fiscais.emitir(LOTE_VAZIO) }
    assert_raises(Almztech::ErroRede) { sdk.notas_fiscais.emitir(LOTE_VAZIO) }
    assert_raises(Almztech::ErroServidor) { sdk.notas_fiscais.cancelar(7) }
    assert_raises(Almztech::ErroServidor) { sdk.notas_fiscais.importar_planilha("1", "\x01", false) }
    assert_equal 4, envio.chamadas.size
    assert_equal "{}", envio.chamadas[2][:corpo]
  end

  def test_4xx_nao_retenta
    envio = EnvioFalso.new.responder(422, fixture("erros/422-validacao.json"))
    assert_raises(Almztech::ErroValidacao) { client(envio).notas_fiscais.consultar_por_codigo(999_999) }
    assert_equal 1, envio.chamadas.size
  end
end

class ErrosTeste < Minitest::Test
  include Auxiliares

  def test_erros_tipados_com_a_mensagem
    envio = EnvioFalso.new
                      .responder(401, fixture("erros/401-sem-token.json"))
                      .responder(403, fixture("erros/403-proibido.json"))
                      .responder(422, fixture("erros/422-validacao.json"))
    sdk = client(envio)
    assert_equal "Token não informado", assert_raises(Almztech::ErroAutenticacao) { sdk.emitentes.consultar }.mensagem
    assert_raises(Almztech::ErroAutorizacao) { sdk.emitentes.consultar }
    assert_equal 422, assert_raises(Almztech::ErroValidacao) { sdk.emitentes.consultar }.status_code
  end

  def test_429_com_retry_after_sem_retentar
    envio = EnvioFalso.new.responder(429, JSON.generate("mensagem" => "Limite de requisições excedido."), { "retry-after" => "60" })
    erro = assert_raises(Almztech::ErroLimiteDeRequisicoes) { client(envio).notas_fiscais.consultar_protocolo("HML-1") }
    assert_equal 60, erro.retry_after
    assert_equal 1, envio.chamadas.size
  end

  def test_400_do_aspnet_vira_validacao_com_os_campos
    problema = { "title" => "x", "errors" => { "notas[0].servicos[0].retemISS" => ["Could not convert."] } }
    envio = EnvioFalso.new.responder(400, JSON.generate(problema))
    erro = assert_raises(Almztech::ErroValidacao) { client(envio).notas_fiscais.emitir(LOTE_VAZIO) }
    assert_equal 400, erro.status_code
    assert_includes erro.mensagem, "retemISS"
  end
end

class PollingTeste < Minitest::Test
  include Auxiliares

  def test_devolve_no_sucesso
    envio = EnvioFalso.new.responder(200, fixture("protocolo-responses/em-fila.json")).responder(200, fixture("protocolo-responses/emissao-sucesso.json"))
    resultado = client(envio).notas_fiscais.aguardar_conclusao("SBOX-1", intervalo_inicial: 0.001, intervalo_maximo: 0.002)
    assert_equal "PROCESSADO_COM_SUCESSO", resultado["status"]
    refute_empty resultado["notas"]
  end

  def test_rejeicao_lanca_com_o_protocolo
    envio = EnvioFalso.new.responder(200, fixture("protocolo-responses/rejeitado.json"))
    erro = assert_raises(Almztech::ErroProtocoloRejeitado) { client(envio).notas_fiscais.aguardar_conclusao("SBOX-1", intervalo_inicial: 0.001) }
    assert_equal "REJEITADO", erro.protocolo["status"]
  end

  def test_estoura_o_tempo_com_o_ultimo_status
    envio = EnvioFalso.new
    50.times { envio.responder(200, EM_FILA) }
    erro = assert_raises(Almztech::ErroTempoDePollingEsgotado) do
      client(envio).notas_fiscais.aguardar_conclusao("HML-1", intervalo_inicial: 0.001, intervalo_maximo: 0.002, timeout_total: 0.005)
    end
    assert_equal "EM_FILA", erro.ultimo_protocolo["status"]
  end
end

class WebhookELogTeste < Minitest::Test
  include Auxiliares

  def test_webhook_classifica_os_tres_eventos
    assert_equal Almztech::Webhook::EMISSAO, Almztech::Webhook.ler(fixture("webhook-payloads/emissao.json")).tipo
    assert_equal Almztech::Webhook::CANCELAMENTO, Almztech::Webhook.ler(fixture("webhook-payloads/cancelamento.json")).tipo
    assert_equal Almztech::Webhook::REJEICAO, Almztech::Webhook.ler(fixture("webhook-payloads/rejeicao.json")).tipo
    assert_equal Almztech::Webhook::EMISSAO, Almztech::Webhook.ler(JSON.parse(fixture("webhook-payloads/emissao.json"))).tipo
  end

  def test_webhook_recusa_vazio_invalido_e_desconhecido
    ["", "{nao json", JSON.generate("protocolo" => "X", "status" => "EM_FILA")].each do |corpo|
      assert_raises(Almztech::ErroWebhook) { Almztech::Webhook.ler(corpo) }
    end
  end

  def test_log_mascara_em_qualquer_profundidade
    corpo = JSON.generate("cpfCnpj" => "123", "senhaCertificado" => "s1", "endereco" => { "senhaPrefeitura" => "s2" },
                          "lista" => [{ "base64" => "AAA" }], "arquivoBase64" => "UEs=")
    mascarado = Almztech::Log.mascarar_corpo_json(corpo)
    %w[s1 s2 AAA UEs=].each { |segredo| refute_includes mascarado, segredo }
    assert_includes mascarado, "123"
    assert_equal "<corpo não-JSON omitido da sanitização de log>", Almztech::Log.mascarar_corpo_json("não é json")
    assert_equal "***1234", Almztech::Log.mascarar_api_key("abcdefgh1234")
  end

  def test_logger_nunca_recebe_chave_nem_senha
    linhas = []
    envio = EnvioFalso.new.responder(200, JSON.generate([{ "razaoSocial" => "X", "cpfCnpj" => "1", "senhaCertificado" => "segredo" }]))
    client(envio, logger: ->(linha) { linhas << linha }, api_key: "chave-secreta-9999").emitentes.consultar
    tudo = linhas.join("\n")
    refute_includes tudo, "chave-secreta"
    refute_includes tudo, "segredo"
    assert_includes tudo, "***9999"
  end
end

class ContratoTeste < Minitest::Test
  include Auxiliares

  def test_emitir_manda_codigos_numericos_omite_nulos_e_headers
    envio = EnvioFalso.new.responder(200, EM_FILA)
    client(envio).notas_fiscais.emitir(
      identificacaoEmitente: "12345678000190",
      enviarParaProducao: false,
      notas: [{
        rps: { serie: "1", tipo: "1", dataEmissao: "2026-10-31", numeroRPS: nil },
        destinatario: { cpfCnpj: "12345678909", razaoSocial: "Cliente", endereco: { codigoIBGEMunicipio: 2_611_606 } },
        servicos: [{ codigo: "0105", descricao: "Teste", valorUnitario: 100.0, retemISS: 2 }],
        observacao: nil
      }]
    )
    nota = envio.corpo["notas"][0]
    assert_equal 2, nota["servicos"][0]["retemISS"]
    refute nota["rps"].key?("numeroRPS")
    refute nota.key?("observacao")
    headers = envio.chamadas[0][:headers]
    assert_equal "api-key-de-teste", headers["x-api-key"]
    assert headers["User-Agent"].start_with?("almztech-ruby-sdk/")
  end

  def test_municipios_id_numerico_e_query
    envio = EnvioFalso.new.responder(200, JSON.generate([{ "id" => 2_611_606, "text" => "2611606 - Recife" }]))
    assert_equal 2_611_606, client(envio).municipios.homologados(codigo_ibge: 2_611_606)[0]["id"]
    assert_equal "/CoberturaNacional/MunicipiosHomologados?codigo=2611606", envio.chamadas[0][:caminho]
  end

  def test_cadastro_devolve_codigo_e_upload_manda_base64
    envio = EnvioFalso.new
                      .responder(200, JSON.generate("mensagem" => "Emitente cadastrado com sucesso.", "codigo" => 42))
                      .responder(200, JSON.generate("mensagem" => "ok", "chaveCertificado" => "abc.pfx"))
    sdk = client(envio)
    assert_equal 42, sdk.emitentes.cadastrar("razaoSocial" => "Empresa", "cpfCnpj" => "1")["codigo"]
    assert_equal "abc.pfx", sdk.emitentes.upload_certificado("1", StringIO.new("\x01\x02\x03"), "senha")["chaveCertificado"]
    assert_equal "AQID", envio.corpo(1)["base64"]
  end

  def test_importar_planilha_manda_base64
    resultado = { "protocolos" => [{ "protocolo" => "HML-9", "status" => "EM_FILA" }], "totalLinhasLidas" => 3,
                  "totalNotasValidas" => 2, "erros" => [{ "linha" => 4, "mensagem" => "CPF inválido" }] }
    envio = EnvioFalso.new.responder(200, JSON.generate(resultado))
    assert_equal 4, client(envio).notas_fiscais.importar_planilha("1", "\x01\x02\x03", false)["erros"][0]["linha"]
    assert_equal "AQID", envio.corpo["arquivoBase64"]
    refute envio.corpo.key?("urlWebhook")
  end

  def test_filtros_como_texto
    envio = EnvioFalso.new.responder(200, JSON.generate("notas" => [], "paginaAtual" => 1, "totalDeRegistros" => 0, "contagensPorAba" => { "all" => 5 }))
    filtros = [
      Almztech::Filtros.periodo("2026-10-01", Date.new(2026, 10, 31)),
      Almztech::Filtros.com_status("EMITIDA", "CANCELADA"),
      Almztech::Filtros.entre_valores(Almztech::Filtros::Campos::VALOR_TOTAL, 10.5, 100)
    ]
    resultado = client(envio).notas_fiscais.consultar_com_filtros(Almztech::Filtros::PRODUCAO, filtros: filtros)
    assert_equal 5, resultado["contagensPorAba"]["all"]
    corpo = envio.corpo
    assert_equal "PRD", corpo["ambiente"]
    refute corpo.key?("filtroRapido")
    assert_equal({ "campo" => "dataEmissao", "operador" => "Entre", "valor" => "2026-10-01", "valorFinal" => "2026-10-31" }, corpo["filtros"][0])
    assert_equal "EMITIDA|CANCELADA", corpo["filtros"][1]["valor"]
    assert_equal "10.5", corpo["filtros"][2]["valor"]
    assert_equal "100", corpo["filtros"][2]["valorFinal"]
  end

  def test_paginas_percorre_ate_o_total
    envio = EnvioFalso.new
                      .responder(200, JSON.generate("notas" => [{ "codigoNota" => 1 }, { "codigoNota" => 2 }], "totalDeRegistros" => 3))
                      .responder(200, JSON.generate("notas" => [{ "codigoNota" => 3 }], "totalDeRegistros" => 3))
    codigos = client(envio).notas_fiscais.paginas(Almztech::Filtros::PRODUCAO, limite_da_pagina: 2).map { |nota| nota["codigoNota"] }
    assert_equal [1, 2, 3], codigos
    assert_equal 2, envio.corpo(1)["pagina"]
  end

  def test_reconsulta_por_chave_e_webhook_com_evento_numerico
    envio = EnvioFalso.new
                      .responder(200, JSON.generate("codigoNota" => 7, "situacao" => "CANCELADA", "protocolo" => "PRD-1"))
                      .responder(200, JSON.generate("codigo" => "g", "evento" => 0, "ativo" => true, "url" => "https://x", "codigoEmitente" => 1))
    sdk = client(envio)
    assert_equal "CANCELADA", sdk.notas_fiscais.reconsultar_por_chave_acesso("NFS26116", true)["situacao"]
    assert_equal({ "enviarParaProducao" => true }, envio.corpo(0))
    sdk.webhooks.criar(codigo_emitente: 1, evento: Almztech::Webhooks::EMISSAO, url: "https://x")
    assert_equal 0, envio.corpo(1)["evento"]
  end

  def test_lista_do_fixture
    envio = EnvioFalso.new.responder(200, fixture("nota-fiscal-resumida/lista-notas.json"))
    notas = client(envio).notas_fiscais.consultar_lista(Almztech::Filtros::PRODUCAO)
    refute_empty notas
    assert_kind_of Integer, notas[0]["codigoNota"]
  end
end
