require "erb"
require "uri"

module Almztech
  # Conteúdo de .pfx/.xlsx: caminho de arquivo (Pathname), String binária ou IO aberto. Uso interno.
  module Arquivo
    module_function

    def base64(arquivo)
      conteudo =
        if arquivo.respond_to?(:read) then arquivo.read
        elsif arquivo.is_a?(Pathname) then File.binread(arquivo)
        else arquivo.to_s
        end
      [conteudo].pack("m0")
    end
  end

  def self.segmento(valor) = ERB::Util.url_encode(valor.to_s)

  # Emissão, consulta, reconsulta, cancelamento, planilha e downloads de NFS-e (rota NFSe/*). Os dados são Hash com as
  # chaves camelCase do JSON da API; chaves com nil não são enviadas.
  class NotasFiscais
    def initialize(transporte)
      @t = transporte
    end

    # POST /NFSe/Emitir. Devolve só o protocolo em fila: acompanhe com +aguardar_conclusao+ ou pelo webhook. Produção
    # exige licença válida e franquia disponível; homologação não consome franquia. *Nunca retentado automaticamente*:
    # a API não tem chave de idempotência e um reenvio pode duplicar a nota.
    def emitir(lote) = @t.json("POST", "NFSe/Emitir", lote)

    # Consulta o protocolo com intervalos crescentes até um status terminal. REJEITADO/ERRO_INTERNO lançam
    # ErroProtocoloRejeitado; estourar +timeout_total+ (segundos) lança ErroTempoDePollingEsgotado com o último protocolo.
    def aguardar_conclusao(protocolo, intervalo_inicial: 2.0, multiplicador: 1.5, intervalo_maximo: 30.0, timeout_total: 300.0, &a_cada_tentativa)
      inicio = Process.clock_gettime(Process::CLOCK_MONOTONIC)
      atraso = intervalo_inicial
      loop do
        ultimo = consultar_protocolo(protocolo)
        a_cada_tentativa&.call(ultimo)

        case ultimo["status"]
        when "PROCESSADO_COM_SUCESSO", "CANCELADO" then return ultimo
        when "REJEITADO", "ERRO_INTERNO" then raise ErroProtocoloRejeitado.new(ultimo)
        end
        raise ErroTempoDePollingEsgotado.new(protocolo, ultimo) if Process.clock_gettime(Process::CLOCK_MONOTONIC) - inicio >= timeout_total

        sleep(atraso)
        atraso = [atraso * multiplicador, intervalo_maximo].min
      end
    end

    def consultar_protocolo(protocolo) = ler("NFSe/Consultar/Protocolo/#{Almztech.segmento(protocolo)}")
    def consultar_por_codigo(codigo_nota) = ler("NFSe/Consultar/#{Integer(codigo_nota)}")

    # Pelo número da NFS-e na prefeitura; havendo mais de uma entre os seus emitentes, vem a mais recente.
    def consultar_por_numero(numero_nota) = ler("NFSe/Consultar/Numero/#{Almztech.segmento(numero_nota)}")

    # As 50 notas mais recentes do ambiente (Filtros::PRODUCAO/HOMOLOGACAO).
    def consultar_lista(ambiente) = ler("NFSe/ConsultarLista/#{Almztech.segmento(ambiente)}")

    # Busca por CNPJ, código, número, razão social, nome fantasia ou protocolo (até 50).
    def consultar_lista_por_termo(ambiente, termo)
      ler("NFSe/ConsultarListaPorTermo/#{Almztech.segmento(ambiente)}/#{Almztech.segmento(termo)}")
    end

    # POST por causa do corpo, mas é leitura (retentado como os GET). Limite real de 100 por página. Devolve "notas",
    # "paginaAtual", "totalDeRegistros", os totais por status e "contagensPorAba".
    def consultar_com_filtros(ambiente, filtros: nil, filtro_rapido: nil, pagina: 1, limite_da_pagina: 50)
      corpo = { "ambiente" => ambiente, "filtroRapido" => filtro_rapido, "filtros" => filtros, "pagina" => pagina, "limiteDaPagina" => limite_da_pagina }
      @t.json("POST", "NFSe/ConsultarComFiltros", corpo, permite_retry: true)
    end

    # Percorre todas as páginas de +consultar_com_filtros+, nota a nota (Enumerator sem bloco).
    def paginas(ambiente, filtros: nil, filtro_rapido: nil, limite_da_pagina: 50)
      return enum_for(:paginas, ambiente, filtros: filtros, filtro_rapido: filtro_rapido, limite_da_pagina: limite_da_pagina) unless block_given?

      pagina = 1
      loop do
        resultado = consultar_com_filtros(ambiente, filtros: filtros, filtro_rapido: filtro_rapido, pagina: pagina, limite_da_pagina: limite_da_pagina)
        notas = resultado["notas"] || []
        notas.each { |nota| yield nota }
        break if notas.empty? || pagina >= (resultado["totalDeRegistros"].to_i / [1, limite_da_pagina].max.to_f).ceil

        pagina += 1
      end
    end

    # Consulta a nota de novo na prefeitura e atualiza a situação local. Repetir é seguro.
    def reconsultar(codigo_nota) = ler("NFSe/Reconsultar/#{Integer(codigo_nota)}")

    # Reconsulta pela chave de acesso do Emissor Nacional; atualiza a nota local de mesmo número.
    def reconsultar_por_chave_acesso(chave_acesso, enviar_para_producao)
      @t.json("POST", "NFSe/Reconsultar/ChaveAcesso/#{Almztech.segmento(chave_acesso)}", { "enviarParaProducao" => enviar_para_producao }, permite_retry: true)
    end

    # POST /NFSe/Cancelar/{codigo}. Assíncrono como a emissão; não retentado.
    def cancelar(codigo_nota, url_webhook: nil) = @t.json("POST", "NFSe/Cancelar/#{Integer(codigo_nota)}", { "urlWebhook" => url_webhook })

    # Bytes do XML; gravar em disco fica com você.
    def baixar_xml(codigo_nota) = @t.bytes("NFSe/XML/#{Integer(codigo_nota)}")
    def baixar_pdf(codigo_nota) = @t.bytes("NFSe/PDF/#{Integer(codigo_nota)}")

    # O .xlsx modelo de +importar_planilha+.
    def baixar_modelo_planilha = @t.bytes("NFSe/ModeloPlanilha")

    # Emite as notas de uma planilha no formato do modelo, com os padrões fiscais do emitente (que precisa do controle
    # automático de RPS). Devolve "protocolos" (um por lote) e "erros" por linha. Não retentado (emite notas).
    # +planilha+: Pathname, String binária ou IO aberto.
    def importar_planilha(identificacao_emitente, planilha, enviar_para_producao, url_webhook: nil)
      corpo = {
        "arquivoBase64" => Arquivo.base64(planilha),
        "identificacaoEmitente" => identificacao_emitente,
        "enviarParaProducao" => enviar_para_producao,
        "urlWebhook" => url_webhook
      }
      @t.json("POST", "NFSe/ImportarPlanilha", corpo)
    end

    private

    def ler(caminho) = @t.json("GET", caminho, permite_retry: true)
  end

  # CRUD de emitente e upload do certificado A1 (rota Emitente/*). +atualizar+ grava todos os campos, inclusive os
  # padrões fiscais: parta do emitente consultado. As leituras trazem senhaCertificado/senhaPrefeitura em claro —
  # nunca logue esses hashes.
  class Emitentes
    def initialize(transporte)
      @t = transporte
    end

    # Até 50 emitentes da conta.
    def consultar = @t.json("GET", "Emitente/Consultar", permite_retry: true)
    def consultar_por_codigo(codigo) = @t.json("GET", "Emitente/Consultar/#{Integer(codigo)}", permite_retry: true)
    def consultar_por_cpf_cnpj(cpf_cnpj) = @t.json("GET", "Emitente/ConsultarCpfCnpj/#{Almztech.segmento(cpf_cnpj)}", permite_retry: true)
    def consultar_por_termo(termo) = @t.json("GET", "Emitente/ConsultaPorTermo/#{Almztech.segmento(termo)}", permite_retry: true)

    # Devolve "mensagem", "cpfCnpj" e o "codigo" do emitente criado.
    def cadastrar(emitente) = @t.json("POST", "Emitente/Cadastrar", emitente)
    def atualizar(emitente) = @t.json("PUT", "Emitente/Atualizar", emitente)
    def excluir(codigo) = @t.json("DELETE", "Emitente/Excluir/#{Integer(codigo)}")

    # Envia o .pfx (Pathname, String binária ou IO), convertido para Base64 aqui. Devolve "chaveCertificado",
    # "vencimento" e "aviso".
    def upload_certificado(cnpj_emissor, certificado_pfx, senha)
      @t.json("POST", "Emitente/UploadCertificado", { "cnpjEmissor" => cnpj_emissor, "base64" => Arquivo.base64(certificado_pfx), "senha" => senha })
    end
  end

  # CRUD e teste de webhook (fisicamente em Emitente/Webhook*). Um registro = um evento.
  class Webhooks
    # Eventos: a API os serializa como número.
    EMISSAO = 0
    CANCELAMENTO = 1
    REJEICAO = 2

    def initialize(transporte)
      @t = transporte
    end

    def obter(codigo) = @t.json("GET", "Emitente/Webhook/#{Almztech.segmento(codigo)}", permite_retry: true)
    def listar_por_emitente(codigo_emitente) = @t.json("GET", "Emitente/Webhooks/#{Integer(codigo_emitente)}", permite_retry: true)

    # Não retentado: a API não barra URL duplicada, e um retry cego criaria dois registros.
    def criar(codigo_emitente:, evento:, url:, ativo: true)
      @t.json("POST", "Emitente/Webhook", { "codigoEmitente" => codigo_emitente, "evento" => evento, "url" => url, "ativo" => ativo })
    end

    def atualizar(webhook) = @t.json("PUT", "Emitente/Webhook", webhook)
    def excluir(codigo) = @t.json("DELETE", "Emitente/Webhook/#{Almztech.segmento(codigo)}")

    # Dispara um payload de exemplo do evento para a URL, sem precisar de webhook cadastrado.
    def testar(url:, evento:) = @t.json("POST", "Emitente/Webhook/Teste", { "url" => url, "evento" => evento })
  end

  # Municípios já homologados (CoberturaNacional/MunicipiosHomologados).
  class Municipios
    def initialize(transporte)
      @t = transporte
    end

    # Pelo código IBGE e/ou por um termo; sem nenhum dos dois vem vazio. Até 20 {"id" => <IBGE>, "text" => ...}.
    def homologados(codigo_ibge: nil, termo_de_busca: nil)
      parametros = { "codigo" => codigo_ibge, "termoDeBusca" => termo_de_busca }.reject { |_, valor| valor.nil? || valor.to_s.empty? }
      sufixo = parametros.empty? ? "" : "?#{URI.encode_www_form(parametros)}"
      @t.json("GET", "CoberturaNacional/MunicipiosHomologados#{sufixo}", permite_retry: true)
    end
  end
end
