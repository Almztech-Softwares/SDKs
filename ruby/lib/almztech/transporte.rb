require "json"
require "net/http"
require "openssl"
require "time"
require "uri"

module Almztech
  # Envio HTTP padrão, com net/http. Um envio injetável é qualquer objeto com
  # +call(metodo, url, headers, corpo, timeout)+ que devolve +[status, headers, corpo]+ inclusive em 4xx/5xx e só
  # lança exceção em falha de rede (proxy corporativo, instrumentação, testes).
  ENVIO_NET_HTTP = lambda do |metodo, url, headers, corpo, timeout|
    uri = URI(url)
    Net::HTTP.start(uri.host, uri.port, use_ssl: uri.scheme == "https", open_timeout: timeout, read_timeout: timeout) do |http|
      requisicao = Net::HTTPGenericRequest.new(metodo, !corpo.nil?, true, uri.request_uri, headers)
      requisicao.body = corpo if corpo
      resposta = http.request(requisicao)
      [resposta.code.to_i, resposta.each_header.to_h, resposta.body.to_s]
    end
  end

  # HTTP do SDK: header x-api-key, retry só em leituras (falha de rede ou 5xx), mapeamento de erros e JSON sem nulos.
  class Transporte
    ERROS_DE_REDE = [IOError, SystemCallError, SocketError, Timeout::Error, OpenSSL::SSL::SSLError].freeze

    # +base_url+: padrão de produção (o ambiente fiscal é por chamada, +enviarParaProducao+). +timeout+: segundos de
    # rede por chamada, não o tempo de processamento da prefeitura. +logger+: recebe só mensagens já mascaradas.
    def initialize(api_key, base_url: "https://api.almztech.com.br", timeout: 30, tentativas: 3, intervalo_inicial: 0.5,
                   multiplicador: 2.0, intervalo_maximo: 10.0, envio_http: nil, logger: nil)
      raise ArgumentError, "api_key é obrigatório." if api_key.nil? || api_key.strip.empty?

      @api_key = api_key
      @base_url = base_url.sub(%r{/+\z}, "")
      @timeout = timeout
      @tentativas = tentativas
      @intervalo_inicial = intervalo_inicial
      @multiplicador = multiplicador
      @intervalo_maximo = intervalo_maximo
      @envio = envio_http || ENVIO_NET_HTTP
      @logger = logger
      log("AlmztechClient configurado (apiKey=#{Log.mascarar_api_key(api_key)}, baseUrl=#{@base_url})")
    end

    def json(metodo, caminho, corpo = nil, permite_retry: false)
      status, headers, texto = enviar(metodo, caminho, corpo, permite_retry)
      texto = texto.to_s.dup.force_encoding(Encoding::UTF_8)
      log("<- #{status} #{metodo} #{caminho} #{Log.mascarar_corpo_json(texto)}")
      raise mapear_erro(status, headers, texto) if status >= 400

      texto.strip.empty? ? nil : JSON.parse(texto)
    end

    def bytes(caminho)
      status, headers, corpo = enviar("GET", caminho, nil, true)
      raise mapear_erro(status, headers, corpo.to_s.dup.force_encoding(Encoding::UTF_8)) if status >= 400

      corpo.to_s.b
    end

    private

    def enviar(metodo, caminho, corpo, permite_retry)
      tentativas = permite_retry ? [1, @tentativas].max : 1
      json = corpo.nil? ? nil : JSON.generate(sem_nulos(corpo))
      headers = { "x-api-key" => @api_key, "User-Agent" => "almztech-ruby-sdk/#{VERSAO_SDK}", "Accept" => "application/json" }
      headers["Content-Type"] = "application/json" if json

      tentativa = 1
      loop do
        log("-> #{metodo} #{caminho} #{Log.mascarar_corpo_json(json)}") if json
        begin
          resposta = @envio.call(metodo, "#{@base_url}/#{caminho}", headers, json, @timeout)
        rescue *ERROS_DE_REDE => erro
          raise ErroRede, "Falha de rede chamando #{metodo} #{caminho}: #{erro.message}" if tentativa >= tentativas

          sleep(atraso(tentativa))
          tentativa += 1
          next
        end

        if resposta[0] >= 500 && tentativa < tentativas
          sleep(atraso(tentativa))
          tentativa += 1
          next
        end
        return resposta
      end
    end

    def atraso(tentativa)
      base = [@intervalo_inicial * (@multiplicador**(tentativa - 1)), @intervalo_maximo].min
      base + rand * base * 0.2
    end

    # Campo numérico da API (tipo não anulável) recusa null com 400: omitir deixa valer o padrão dela.
    def sem_nulos(valor)
      case valor
      when Hash then valor.each_with_object({}) { |(chave, item), saida| saida[chave] = sem_nulos(item) unless item.nil? }
      when Array then valor.map { |item| sem_nulos(item) }
      else valor
      end
    end

    def log(mensagem)
      @logger&.call(mensagem)
    end

    def mapear_erro(status, headers, corpo)
      mensagem = extrair_mensagem(corpo)
      case status
      when 400, 422 then ErroValidacao.new(status, mensagem, corpo)
      when 401 then ErroAutenticacao.new(status, mensagem, corpo)
      when 403 then ErroAutorizacao.new(status, mensagem, corpo)
      when 429 then ErroLimiteDeRequisicoes.new(mensagem, ler_retry_after(headers), corpo)
      else status >= 500 ? ErroServidor.new(status, mensagem, corpo) : ErroApi.new(status, mensagem, corpo)
      end
    end

    def ler_retry_after(headers)
      valor = headers.find { |nome, _| nome.to_s.casecmp?("retry-after") }&.last
      valor = valor.first if valor.is_a?(Array)
      return nil if valor.nil? || valor.to_s.strip.empty?
      return valor.to_i if valor.to_s.match?(/\A\d+\z/)

      [(Time.httpdate(valor) - Time.now).to_i, 0].max
    rescue ArgumentError
      nil
    end

    # {mensagem} é o formato de erro da API; o 400 do ASP.NET vem como ProblemDetails, com +errors+ por campo.
    def extrair_mensagem(corpo)
      return "(sem corpo de resposta)" if corpo.strip.empty?

      dados = JSON.parse(corpo)
      if dados.is_a?(Hash)
        return dados["mensagem"] if dados["mensagem"].is_a?(String)

        if dados["errors"].is_a?(Hash)
          return dados["errors"].flat_map { |campo, erros| Array(erros).map { |erro| "#{campo}: #{erro}" } }.join("; ")
        end
      end
      corpo
    rescue JSON::ParserError
      corpo
    end
  end
end
