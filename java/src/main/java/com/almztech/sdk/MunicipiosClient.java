package com.almztech.sdk;

import com.almztech.sdk.modelos.MunicipioHomologado;
import java.util.ArrayList;
import java.util.List;

/** Municípios já homologados ({@code CoberturaNacional/MunicipiosHomologados}). */
public class MunicipiosClient {
	private final Transporte transporte;

	MunicipiosClient(Transporte transporte) {
		this.transporte = transporte;
	}

	/** Pelo código IBGE e/ou por um termo (parte do nome ou do código); sem nenhum dos dois, vem vazio. Até 20. */
	public List<MunicipioHomologado> homologados(Integer codigoIbge, String termoDeBusca) {
		List<String> parametros = new ArrayList<>();
		if (codigoIbge != null)
			parametros.add("codigo=" + codigoIbge);
		if (termoDeBusca != null && !termoDeBusca.isEmpty())
			parametros.add("termoDeBusca=" + Urls.segmento(termoDeBusca));
		String sufixo = parametros.isEmpty() ? "" : "?" + String.join("&", parametros);
		return transporte.json("GET", "CoberturaNacional/MunicipiosHomologados" + sufixo, null, true, Tipos.listaDe(MunicipioHomologado.class));
	}
}
