module Almztech
  # Base de todas as exceções do SDK (DESIGN.md seção 3).
  class Erro < StandardError; end

  # Erro devolvido pela API ({"mensagem": "..."}). Não há código estruturado: +mensagem+ é texto livre.
  class ErroApi < Erro
    attr_reader :status_code, :mensagem, :corpo_bruto

    def initialize(status_code, mensagem, corpo_bruto = nil)
      super("A API da Almztech retornou #{status_code}: #{mensagem}")
      @status_code = status_code
      @mensagem = mensagem
      @corpo_bruto = corpo_bruto
    end
  end

  # 422 (regra de negócio, inclusive franquia do plano esgotada) ou 400 (corpo recusado, com os erros por campo).
  class ErroValidacao < ErroApi; end

  # 401 — token ausente, inválido ou revogado, ou licença inválida para produção. Só a mensagem diferencia.
  class ErroAutenticacao < ErroApi; end

  # 403 — papel sem permissão para o endpoint.
  class ErroAutorizacao < ErroApi; end

  # 429 — limite de requisições. Não é retentado sozinho: +retry_after+ (segundos) diz quanto esperar.
  class ErroLimiteDeRequisicoes < ErroApi
    attr_reader :retry_after

    def initialize(mensagem, retry_after, corpo_bruto = nil)
      super(429, mensagem, corpo_bruto)
      @retry_after = retry_after
    end
  end

  # 5xx — erro interno; em produção a mensagem pode vir genérica.
  class ErroServidor < ErroApi; end

  # Falha de rede, timeout ou DNS antes de qualquer resposta HTTP.
  class ErroRede < Erro; end

  # O polling estourou o tempo total; +ultimo_protocolo+ permite continuar manualmente.
  class ErroTempoDePollingEsgotado < Erro
    attr_reader :protocolo, :ultimo_protocolo

    def initialize(protocolo, ultimo_protocolo)
      status = ultimo_protocolo && ultimo_protocolo["status"] || "desconhecido"
      super("O protocolo #{protocolo} não concluiu no tempo de espera (último status: #{status}).")
      @protocolo = protocolo
      @ultimo_protocolo = ultimo_protocolo
    end
  end

  # O protocolo terminou em REJEITADO ou ERRO_INTERNO; o motivo está em +protocolo["mensagem"]+.
  class ErroProtocoloRejeitado < Erro
    attr_reader :protocolo

    def initialize(protocolo)
      super("O protocolo #{protocolo['protocolo']} terminou em #{protocolo['status']}: #{protocolo['mensagem'] || '(sem mensagem)'}")
      @protocolo = protocolo
    end
  end

  # Payload de webhook não reconhecido.
  class ErroWebhook < Erro; end
end
