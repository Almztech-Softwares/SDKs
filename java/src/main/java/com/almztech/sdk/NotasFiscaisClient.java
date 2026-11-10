package com.almztech.sdk;

import com.almztech.sdk.erros.AlmztechPollingTimeoutException;
import com.almztech.sdk.erros.AlmztechProtocoloRejeitadoException;
import com.almztech.sdk.modelos.NotaFiscal;
import com.almztech.sdk.modelos.ParametrosFiltros;
import com.almztech.sdk.modelos.ProtocoloNotaResponse;
import com.almztech.sdk.modelos.ProtocoloResponse;
import com.almztech.sdk.modelos.RecepcaoLote;
import com.almztech.sdk.modelos.ResultadoConsultaComFiltros;
import com.almztech.sdk.modelos.ResultadoImportacaoPlanilha;
import com.almztech.sdk.modelos.StatusProtocolo;
import java.util.Base64;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.NoSuchElementException;

/** Emissão, consulta, reconsulta, cancelamento, planilha e downloads de NFS-e (rota {@code NFSe/*}). */
public class NotasFiscaisClient {
	private final Transporte transporte;

	NotasFiscaisClient(Transporte transporte) {
		this.transporte = transporte;
	}

	/**
	 * {@code POST /NFSe/Emitir}. Devolve só o protocolo em fila: acompanhe com {@link #aguardarConclusao} ou pelo
	 * webhook. Produção exige licença válida e franquia disponível; homologação não consome franquia. <b>Nunca
	 * retentado automaticamente</b> — a API não tem chave de idempotência e um reenvio pode duplicar a nota.
	 */
	public ProtocoloResponse emitir(RecepcaoLote lote) {
		return transporte.json("POST", "NFSe/Emitir", lote, false, ProtocoloResponse.class);
	}

	/** {@link #aguardarConclusao(String, AguardarConclusaoOptions)} com as opções padrão (até 5 minutos). */
	public ProtocoloResponse aguardarConclusao(String protocolo) {
		return aguardarConclusao(protocolo, new AguardarConclusaoOptions());
	}

	/**
	 * Consulta o protocolo com intervalos crescentes até um status terminal. {@code REJEITADO}/{@code ERRO_INTERNO}
	 * lançam {@link AlmztechProtocoloRejeitadoException}; estourar o tempo lança {@link AlmztechPollingTimeoutException}.
	 */
	public ProtocoloResponse aguardarConclusao(String protocolo, AguardarConclusaoOptions opcoes) {
		long inicio = System.nanoTime();
		long atraso = opcoes.intervaloInicial.toMillis();
		while (true) {
			ProtocoloResponse ultimo = consultarProtocolo(protocolo);
			if (opcoes.aCadaTentativa != null)
				opcoes.aCadaTentativa.accept(ultimo);

			if (ultimo.status == StatusProtocolo.PROCESSADO_COM_SUCESSO || ultimo.status == StatusProtocolo.CANCELADO)
				return ultimo;
			if (ultimo.status == StatusProtocolo.REJEITADO || ultimo.status == StatusProtocolo.ERRO_INTERNO)
				throw new AlmztechProtocoloRejeitadoException(ultimo);
			if ((System.nanoTime() - inicio) / 1_000_000 >= opcoes.timeoutTotal.toMillis())
				throw new AlmztechPollingTimeoutException(protocolo, ultimo);

			try {
				Thread.sleep(atraso);
			} catch (InterruptedException erro) {
				Thread.currentThread().interrupt();
				throw new AlmztechPollingTimeoutException(protocolo, ultimo);
			}
			atraso = Math.min((long) (atraso * opcoes.backoffMultiplicador), opcoes.intervaloMaximo.toMillis());
		}
	}

	public ProtocoloResponse consultarProtocolo(String protocolo) {
		return ler("NFSe/Consultar/Protocolo/" + Urls.segmento(protocolo), ProtocoloResponse.class);
	}

	public ProtocoloNotaResponse consultarPorCodigo(int codigoNota) {
		return ler("NFSe/Consultar/" + codigoNota, ProtocoloNotaResponse.class);
	}

	/** Pelo número da NFS-e na prefeitura; havendo mais de uma entre os seus emitentes, vem a mais recente. */
	public ProtocoloNotaResponse consultarPorNumero(String numeroNota) {
		return ler("NFSe/Consultar/Numero/" + Urls.segmento(numeroNota), ProtocoloNotaResponse.class);
	}

	/** As 50 notas mais recentes do ambiente ({@code Ambiente.PRODUCAO}/{@code HOMOLOGACAO}). */
	public List<NotaFiscal> consultarLista(String ambiente) {
		return transporte.json("GET", "NFSe/ConsultarLista/" + Urls.segmento(ambiente), null, true, Tipos.listaDe(NotaFiscal.class));
	}

	/** Busca por CNPJ, código, número, razão social, nome fantasia ou protocolo (até 50). */
	public List<NotaFiscal> consultarListaPorTermo(String ambiente, String termo) {
		String caminho = "NFSe/ConsultarListaPorTermo/" + Urls.segmento(ambiente) + "/" + Urls.segmento(termo);
		return transporte.json("GET", caminho, null, true, Tipos.listaDe(NotaFiscal.class));
	}

	/** POST por causa do corpo, mas é leitura: retentado como os GET. */
	public ResultadoConsultaComFiltros consultarComFiltros(ParametrosFiltros parametros) {
		return transporte.json("POST", "NFSe/ConsultarComFiltros", parametros, true, ResultadoConsultaComFiltros.class);
	}

	/** Percorre todas as páginas de {@link #consultarComFiltros}, nota a nota, buscando cada página sob demanda. */
	public Iterable<NotaFiscal> paginas(ParametrosFiltros parametros) {
		return () -> new Iterator<NotaFiscal>() {
			private int pagina = Math.max(1, parametros.pagina);
			private Iterator<NotaFiscal> atual;
			private boolean ultimaPagina;

			@Override
			public boolean hasNext() {
				while ((atual == null || !atual.hasNext()) && !ultimaPagina) {
					ParametrosFiltros pedido = copiar(parametros, pagina);
					ResultadoConsultaComFiltros resultado = consultarComFiltros(pedido);
					List<NotaFiscal> notas = resultado.notas == null ? List.of() : resultado.notas;
					int totalDePaginas = (int) Math.ceil(resultado.totalDeRegistros / (double) Math.max(1, pedido.limiteDaPagina));
					ultimaPagina = notas.isEmpty() || pagina >= totalDePaginas;
					atual = notas.iterator();
					pagina++;
				}
				return atual != null && atual.hasNext();
			}

			@Override
			public NotaFiscal next() {
				if (!hasNext())
					throw new NoSuchElementException();
				return atual.next();
			}
		};
	}

	/** Consulta a nota de novo na prefeitura e atualiza a situação local. Repetir é seguro. */
	public ProtocoloNotaResponse reconsultar(int codigoNota) {
		return ler("NFSe/Reconsultar/" + codigoNota, ProtocoloNotaResponse.class);
	}

	/** Reconsulta pela chave de acesso do Emissor Nacional; atualiza a nota local de mesmo número. */
	public ProtocoloNotaResponse reconsultarPorChaveAcesso(String chaveAcesso, boolean enviarParaProducao) {
		Map<String, Object> corpo = new LinkedHashMap<>();
		corpo.put("enviarParaProducao", enviarParaProducao);
		return transporte.json("POST", "NFSe/Reconsultar/ChaveAcesso/" + Urls.segmento(chaveAcesso), corpo, true, ProtocoloNotaResponse.class);
	}

	/** {@code POST /NFSe/Cancelar/{codigo}}. Assíncrono como a emissão; não retentado. */
	public ProtocoloResponse cancelar(int codigoNota, String urlWebhook) {
		Map<String, Object> corpo = new LinkedHashMap<>();
		corpo.put("urlWebhook", urlWebhook);
		return transporte.json("POST", "NFSe/Cancelar/" + codigoNota, corpo, false, ProtocoloResponse.class);
	}

	public ProtocoloResponse cancelar(int codigoNota) {
		return cancelar(codigoNota, null);
	}

	/** Bytes do XML; gravar em disco fica com você. */
	public byte[] baixarXml(int codigoNota) {
		return transporte.bytes("NFSe/XML/" + codigoNota);
	}

	public byte[] baixarPdf(int codigoNota) {
		return transporte.bytes("NFSe/PDF/" + codigoNota);
	}

	/** O {@code .xlsx} modelo de {@link #importarPlanilha}. */
	public byte[] baixarModeloPlanilha() {
		return transporte.bytes("NFSe/ModeloPlanilha");
	}

	/**
	 * Emite as notas de uma planilha no formato do modelo, com os padrões fiscais do emitente (que precisa do controle
	 * automático de RPS). Um protocolo por lote e os erros por linha. Não retentado (emite notas). Para ler de arquivo
	 * ou stream, use {@link Arquivos}.
	 */
	public ResultadoImportacaoPlanilha importarPlanilha(String identificacaoEmitente, byte[] planilhaXlsx, boolean enviarParaProducao, String urlWebhook) {
		Map<String, Object> corpo = new LinkedHashMap<>();
		corpo.put("arquivoBase64", Base64.getEncoder().encodeToString(planilhaXlsx));
		corpo.put("identificacaoEmitente", identificacaoEmitente);
		corpo.put("enviarParaProducao", enviarParaProducao);
		corpo.put("urlWebhook", urlWebhook);
		return transporte.json("POST", "NFSe/ImportarPlanilha", corpo, false, ResultadoImportacaoPlanilha.class);
	}

	private <T> T ler(String caminho, Class<T> tipo) {
		return transporte.json("GET", caminho, null, true, tipo);
	}

	private static ParametrosFiltros copiar(ParametrosFiltros original, int pagina) {
		ParametrosFiltros copia = new ParametrosFiltros();
		copia.ambiente = original.ambiente;
		copia.filtroRapido = original.filtroRapido;
		copia.filtros = original.filtros;
		copia.limiteDaPagina = original.limiteDaPagina;
		copia.pagina = pagina;
		return copia;
	}
}
