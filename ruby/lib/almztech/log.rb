require "json"

module Almztech
  # Mascaramento central de log (DESIGN.md seção 6). Cobre request e response: +senhaCertificado+ e
  # +senhaPrefeitura+ vêm em claro inclusive nas leituras de emitente. Todo log automático do SDK passa por aqui.
  module Log
    CAMPOS_SENSIVEIS = %w[apikey senha senhacertificado senhaprefeitura base64 arquivobase64 login].freeze
    MASCARA = "***".freeze

    module_function

    # Mostra só os últimos 4 caracteres da api key.
    def mascarar_api_key(api_key)
      return MASCARA if api_key.nil? || api_key.empty?

      MASCARA + (api_key.length > 4 ? api_key[-4..] : api_key)
    end

    # Cópia do JSON com todo campo sensível mascarado. Corpo não-JSON nunca é devolvido cru.
    def mascarar_corpo_json(corpo)
      return "" if corpo.nil? || corpo.strip.empty?

      JSON.generate(mascarar(JSON.parse(corpo)))
    rescue JSON::ParserError
      "<corpo não-JSON omitido da sanitização de log>"
    end

    def mascarar(valor)
      case valor
      when Array then valor.map { |item| mascarar(item) }
      when Hash then valor.to_h { |chave, item| [chave, CAMPOS_SENSIVEIS.include?(chave.to_s.downcase) ? MASCARA : mascarar(item)] }
      else valor
      end
    end
  end
end
