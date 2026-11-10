using System.Text.Json;
using System.Text.Json.Nodes;

namespace Almztech.Sdk.Logging;

/// <summary>
/// Sanitização central de log — ver DESIGN.md seção 6. Cobre tanto request quanto response
/// bodies, porque <c>senhaCertificado</c>/<c>senhaPrefeitura</c> vêm em texto puro inclusive em
/// respostas de leitura (confirmado empiricamente). Nunca depender de sanitizar em cada call site;
/// todo log automático do SDK passa por aqui primeiro.
/// </summary>
public static class LogSanitizer
{
    private static readonly HashSet<string> CamposSensiveis = new(StringComparer.OrdinalIgnoreCase)
    {
        "apiKey",
        "senha",
        "senhaCertificado",
        "senhaPrefeitura",
        "base64",
        "arquivoBase64",
        "login",
    };

    private const string Mascara = "***";

    /// <summary>Mascara o header x-api-key mostrando só os últimos 4 caracteres.</summary>
    public static string SanitizarApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return Mascara;

        var sufixo = apiKey.Length > 4 ? apiKey[^4..] : apiKey;
        return $"{Mascara}{sufixo}";
    }

    /// <summary>
    /// Retorna uma cópia do JSON com todo campo sensível (em qualquer profundidade) mascarado.
    /// Corpo malformado ou não-JSON retorna uma string fixa em vez do conteúdo original (nunca
    /// vazar dado bruto não reconhecido).
    /// </summary>
    public static string SanitizarCorpoJson(string? corpoJson)
    {
        if (string.IsNullOrWhiteSpace(corpoJson))
            return string.Empty;

        try
        {
            var node = JsonNode.Parse(corpoJson);
            MascararRecursivo(node);
            return node?.ToJsonString() ?? string.Empty;
        }
        catch (JsonException)
        {
            return "<corpo não-JSON omitido da sanitização de log>";
        }
    }

    private static void MascararRecursivo(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var chave in obj.Select(kv => kv.Key).ToList())
                {
                    if (CamposSensiveis.Contains(chave))
                        obj[chave] = Mascara;
                    else
                        MascararRecursivo(obj[chave]);
                }

                break;
            case JsonArray arr:
                foreach (var item in arr)
                    MascararRecursivo(item);

                break;
        }
    }
}
