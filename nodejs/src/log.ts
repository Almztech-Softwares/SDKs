/*
  Mascaramento central de log (DESIGN.md seção 6). Cobre request e response: senhaCertificado e
  senhaPrefeitura vêm em claro inclusive nas leituras de emitente. Todo log automático do SDK passa por aqui.
*/

const CAMPOS_SENSIVEIS = new Set(["apikey", "senha", "senhacertificado", "senhaprefeitura", "base64", "arquivobase64", "login"]);
const MASCARA = "***";

/** Mostra só os últimos 4 caracteres da api key. */
export function mascararApiKey(apiKey: string): string {
	return apiKey ? `${MASCARA}${apiKey.length > 4 ? apiKey.slice(-4) : apiKey}` : MASCARA;
}

/** Cópia do JSON com todo campo sensível mascarado, em qualquer profundidade. Corpo não-JSON nunca é devolvido cru. */
export function mascararCorpoJson(corpo: string | undefined): string {
	if (!corpo || !corpo.trim()) return "";

	try {
		return JSON.stringify(mascarar(JSON.parse(corpo)));
	} catch {
		return "<corpo não-JSON omitido da sanitização de log>";
	}
}

function mascarar(valor: unknown): unknown {
	if (Array.isArray(valor)) return valor.map(mascarar);
	if (valor === null || typeof valor !== "object") return valor;

	return Object.fromEntries(
		Object.entries(valor).map(([chave, item]) => [chave, CAMPOS_SENSIVEIS.has(chave.toLowerCase()) ? MASCARA : mascarar(item)]),
	);
}
