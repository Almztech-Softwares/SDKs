package com.almztech.sdk.modelos;

import java.math.BigDecimal;

/** Emitente. {@code atualizar} grava todos os campos, inclusive os padrões fiscais: atualize a partir do emitente
 * consultado. Senhas, certificado, controle automático e sequências de RPS mantêm o valor gravado quando vêm
 * vazios. {@code senhaCertificado}/{@code senhaPrefeitura} vêm em claro nas leituras — nunca logar. */
public class Emitente {
	public Integer codigo;
	public String razaoSocial;
	public String nomeFantasia;
	public String cpfCnpj;
	public String inscricaoEstadual;
	public String inscricaoMunicipal;
	public String telefone;
	public String email;
	public String chaveCertificado;
	public String senhaCertificado;
	public String vencimentoCertificado;
	public String usuarioPrefeitura;
	public String senhaPrefeitura;
	public String codigoCnae;
	public Boolean incentivadorCultural;
	public String incentivoFiscal;
	public Boolean optanteSimples;
	public Integer regimeTributacao;
	/** Ligado, a API numera o RPS. */
	public Boolean controleAutomaticoRPS;
	public Integer sequenciaRPSProducao;
	public Integer sequenciaRPSHomologacao;
	/** {@code false} mantém a sequência gravada mesmo com valor informado. */
	public Boolean sequenciaRPSProducaoAlterada;
	public Boolean sequenciaRPSHomologacaoAlterada;
	public Boolean ativo;
	public Integer naturezaOperacaoPadrao;
	public Boolean calculaRetencoesFederaisPadrao;
	public String tipoRpsPadrao;
	public String serieRpsPadrao;
	public Integer exigibilidadeIssPadrao;
	public Integer tipoTributacaoPadrao;
	public Integer issRetidoPadrao;
	public BigDecimal aliquotaIssPadrao;
	public String codigoServicoPadrao;
	public EnderecoEmitente endereco;
}
