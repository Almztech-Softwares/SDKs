package com.almztech.sdk.modelos;

/** Endereço do tomador. */
public class Endereco {
	public String logradouro;
	public String numero;
	public String complemento;
	public String bairro;
	public String cep;
	/** Código BACEN do país; a API assume 1058 (Brasil). */
	public Integer codigoPais;
	public Integer codigoIBGEUF;
	public Integer codigoIBGEMunicipio;
	public String siglaUF;
	public String descricaoUF;
	public String descricaoMunicipio;
}
