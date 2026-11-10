require "pathname"
require_relative "almztech/versao"
require_relative "almztech/erros"
require_relative "almztech/log"
require_relative "almztech/transporte"
require_relative "almztech/filtros"
require_relative "almztech/recursos"
require_relative "almztech/webhook"

# SDK oficial Ruby da Almztech (DESIGN.md).
#
#   client = Almztech::Client.new(ENV.fetch("ALMZTECH_API_KEY"))
#   protocolo = client.notas_fiscais.emitir(lote)
#   resultado = client.notas_fiscais.aguardar_conclusao(protocolo["protocolo"])
module Almztech
  # Client raiz. Crie um por aplicação e reaproveite. A api key está em "Primeiros passos" no Portal; nunca guarde
  # login e senha no seu sistema. As opções (+base_url+, +timeout+, +tentativas+, +envio_http+, +logger+...) são as
  # de Transporte.
  class Client
    attr_reader :notas_fiscais, :emitentes, :webhooks, :municipios

    def initialize(api_key, **opcoes)
      transporte = Transporte.new(api_key, **opcoes)
      @notas_fiscais = NotasFiscais.new(transporte)
      @emitentes = Emitentes.new(transporte)
      @webhooks = Webhooks.new(transporte)
      @municipios = Municipios.new(transporte)
    end
  end
end
