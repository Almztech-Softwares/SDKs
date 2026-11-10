require "json"

module Almztech
  # Leitura do payload recebido no seu endpoint de webhook (DESIGN.md seção 5). Não verifica autenticidade: a API
  # não assina o webhook. Proteger o endpoint (token secreto na URL, allowlist de IP, mTLS) é responsabilidade de quem
  # o recebe.
  module Webhook
    EMISSAO = :emissao
    CANCELAMENTO = :cancelamento
    REJEICAO = :rejeicao

    # Evento classificado: +tipo+ (EMISSAO, CANCELAMENTO ou REJEICAO) e o payload completo (mesmo schema do polling).
    Evento = Struct.new(:tipo, :protocolo, keyword_init: true)

    module_function

    # "notas" não vazio → Emissão; senão "dadosCancelamento" → Cancelamento; senão status REJEITADO → Rejeição (motivo
    # em "mensagem"); senão lança ErroWebhook. +corpo+: o corpo cru do POST ou um Hash já decodificado.
    def ler(corpo)
      protocolo =
        if corpo.is_a?(Hash)
          corpo
        else
          raise ErroWebhook, "Corpo do webhook vazio." if corpo.nil? || corpo.strip.empty?

          begin
            JSON.parse(corpo)
          rescue JSON::ParserError
            raise ErroWebhook, "Corpo do webhook não é um JSON válido."
          end
        end
      raise ErroWebhook, "Corpo do webhook não é um objeto." unless protocolo.is_a?(Hash)

      return Evento.new(tipo: EMISSAO, protocolo: protocolo) if protocolo["notas"].is_a?(Array) && !protocolo["notas"].empty?
      return Evento.new(tipo: CANCELAMENTO, protocolo: protocolo) if protocolo["dadosCancelamento"]
      return Evento.new(tipo: REJEICAO, protocolo: protocolo) if protocolo["status"] == "REJEITADO"

      raise ErroWebhook, "Payload de webhook não reconhecido (protocolo='#{protocolo['protocolo']}', status='#{protocolo['status']}'): " \
                         "sem 'notas', sem 'dadosCancelamento' e status diferente de REJEITADO."
    end
  end
end
