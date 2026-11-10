package com.almztech.sdk.modelos;

import java.util.List;

/** Uma nota do lote. */
public class NotaFiscalResumida {
	public IdentificacaoDaNota rps;
	public Destinatario destinatario;
	public List<Servico> servicos;
	public List<CondicaoPagamento> condicoesPagamentos;
	public String observacao;
	/** Nome com o typo do schema real ({@code Subtituicao}) — não corrigir, quebraria o wire format. */
	public SubtituicaoRps subtituicaoRps;
}
