package com.almztech.sdk.modelos;

/** Tomador do serviço. */
public class Destinatario {
	public String cpfCnpj;
	public String razaoSocial;
	public String nomeFantasia;
	public String inscricaoEstadual;
	public String inscricaoMunicipal;
	public String telefone;
	/** Precisa do {@code codigoIBGEMunicipio}: sem ele a nota falha depois, no processamento assíncrono. */
	public Endereco endereco;
}
