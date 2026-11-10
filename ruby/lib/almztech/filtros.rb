require "date"

module Almztech
  # Filtros de +consultar_com_filtros+ (DESIGN.md seção 7). O valor vai sempre como texto: datas +yyyy-MM-dd+ e
  # decimais com ponto. Campo desconhecido ou valor vazio é ignorado pela API (não filtra), por isso prefira as
  # constantes e os construtores daqui.
  module Filtros
    PRODUCAO = "PRD".freeze
    HOMOLOGACAO = "HML".freeze

    module Campos
      PROTOCOLO = "protocolo".freeze
      CODIGO_NOTA = "codigoNota".freeze
      NOME_EMITENTE = "nomeEmitente".freeze
      CPF_CNPJ_EMITENTE = "cpfCnpjEmitente".freeze
      NUMERO_RPS = "numeroRps".freeze
      SERIE_RPS = "serieRps".freeze
      LOTE_RPS = "loteRps".freeze
      VALOR_TOTAL = "valorTotal".freeze
      # Data yyyy-MM-dd.
      DATA_EMISSAO = "dataEmissao".freeze
      # Vários status separados por |; só com Igual.
      STATUS = "status".freeze
    end

    # Operadores (OperadorFiltro da API, pelo nome).
    OPERADORES = %w[Igual Diferente MaiorQue MaiorOuIgual MenorQue MenorOuIgual Entre Contem IniciaCom TerminaCom].freeze

    module_function

    def filtro(campo, operador, valor, valor_final = nil)
      item = { "campo" => campo, "operador" => operador, "valor" => valor }
      item["valorFinal"] = valor_final unless valor_final.nil?
      item
    end

    def texto(campo, operador, valor) = filtro(campo, operador, valor.to_s)
    def numero(campo, operador, valor) = filtro(campo, operador, decimal(valor))
    def entre_valores(campo, inicio, fim) = filtro(campo, "Entre", decimal(inicio), decimal(fim))
    def data(operador, valor) = filtro(Campos::DATA_EMISSAO, operador, data_iso(valor))
    def periodo(inicio, fim) = filtro(Campos::DATA_EMISSAO, "Entre", data_iso(inicio), data_iso(fim))
    def com_status(*status) = filtro(Campos::STATUS, "Igual", status.join("|"))

    # Sempre ponto decimal, independente do locale, sem notação científica.
    def decimal(valor)
      return valor.to_s if valor.is_a?(Integer) || valor.is_a?(String)

      format("%.10f", valor).sub(/0+\z/, "").sub(/\.\z/, "")
    end

    def data_iso(valor)
      valor.respond_to?(:strftime) ? valor.strftime("%Y-%m-%d") : valor.to_s
    end
  end
end
