package com.almztech.sdk;

import com.almztech.sdk.modelos.Emitente;
import com.almztech.sdk.modelos.Mensagem;
import com.almztech.sdk.modelos.ResultadoGravacaoEmitente;
import com.almztech.sdk.modelos.ResultadoUploadCertificado;
import java.util.Base64;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** CRUD de emitente e upload do certificado A1 (rota {@code Emitente/*}). */
public class EmitentesClient {
	private final Transporte transporte;

	EmitentesClient(Transporte transporte) {
		this.transporte = transporte;
	}

	/** Até 50 emitentes da conta. */
	public List<Emitente> consultar() {
		return transporte.json("GET", "Emitente/Consultar", null, true, Tipos.listaDe(Emitente.class));
	}

	public Emitente consultarPorCodigo(int codigo) {
		return transporte.json("GET", "Emitente/Consultar/" + codigo, null, true, Emitente.class);
	}

	public Emitente consultarPorCpfCnpj(String cpfCnpj) {
		return transporte.json("GET", "Emitente/ConsultarCpfCnpj/" + Urls.segmento(cpfCnpj), null, true, Emitente.class);
	}

	public List<Emitente> consultarPorTermo(String termo) {
		return transporte.json("GET", "Emitente/ConsultaPorTermo/" + Urls.segmento(termo), null, true, Tipos.listaDe(Emitente.class));
	}

	/** Devolve o código do emitente criado. */
	public ResultadoGravacaoEmitente cadastrar(Emitente emitente) {
		return transporte.json("POST", "Emitente/Cadastrar", emitente, false, ResultadoGravacaoEmitente.class);
	}

	/** Grava todos os campos, inclusive os padrões fiscais: parta do emitente consultado (ver {@link Emitente}). */
	public ResultadoGravacaoEmitente atualizar(Emitente emitente) {
		return transporte.json("PUT", "Emitente/Atualizar", emitente, false, ResultadoGravacaoEmitente.class);
	}

	public Mensagem excluir(int codigo) {
		return transporte.json("DELETE", "Emitente/Excluir/" + codigo, null, false, Mensagem.class);
	}

	/** Envia o {@code .pfx}, convertido para Base64 aqui. Para ler de arquivo ou stream, use {@link Arquivos}. */
	public ResultadoUploadCertificado uploadCertificado(String cnpjEmissor, byte[] certificadoPfx, String senha) {
		Map<String, Object> corpo = new LinkedHashMap<>();
		corpo.put("cnpjEmissor", cnpjEmissor);
		corpo.put("base64", Base64.getEncoder().encodeToString(certificadoPfx));
		corpo.put("senha", senha);
		return transporte.json("POST", "Emitente/UploadCertificado", corpo, false, ResultadoUploadCertificado.class);
	}
}
