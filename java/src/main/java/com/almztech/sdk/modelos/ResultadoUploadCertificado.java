package com.almztech.sdk.modelos;

/** Retorno do upload do certificado. */
public class ResultadoUploadCertificado {
	public String mensagem;
	public String chaveCertificado;
	public String vencimento;
	/** Alerta não bloqueante (ex.: vencimento próximo), quando houver. */
	public String aviso;
}
