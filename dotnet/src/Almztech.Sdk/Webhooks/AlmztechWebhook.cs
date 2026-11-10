using System.Text.Json;
using Almztech.Sdk.Exceptions;
using Almztech.Sdk.Http;
using Almztech.Sdk.Models;

namespace Almztech.Sdk.Webhooks;

public enum TipoEventoWebhook
{
    Emissao,
    Cancelamento,
    Rejeicao,
}

/// <summary>Evento tipado retornado por <see cref="AlmztechWebhook.Parse"/>.</summary>
public class EventoWebhook
{
    internal EventoWebhook(TipoEventoWebhook tipo, ProtocoloResponse protocolo)
    {
        Tipo = tipo;
        Protocolo = protocolo;
    }

    public TipoEventoWebhook Tipo { get; }

    /// <summary>Payload completo desserializado, para acesso a campos além dos atalhos abaixo.</summary>
    public ProtocoloResponse Protocolo { get; }

    /// <summary>Preenchido só quando <see cref="Tipo"/> é <see cref="TipoEventoWebhook.Emissao"/>.</summary>
    public IReadOnlyList<ProtocoloNotaResponse> Notas => Protocolo.Notas ?? (IReadOnlyList<ProtocoloNotaResponse>)Array.Empty<ProtocoloNotaResponse>();

    /// <summary>Preenchido só quando <see cref="Tipo"/> é <see cref="TipoEventoWebhook.Cancelamento"/>.</summary>
    public ProtocoloCancelamentoResponse? DadosCancelamento => Protocolo.DadosCancelamento;

    /// <summary>Motivo da rejeição quando <see cref="Tipo"/> é <see cref="TipoEventoWebhook.Rejeicao"/>.</summary>
    public string? Mensagem => Protocolo.Mensagem;
}

/// <summary>
/// Helper de parsing do payload recebido no endpoint de webhook do parceiro — ver DESIGN.md
/// seção 5. Não faz nenhuma verificação de autenticidade/origem do payload: a API não envia
/// assinatura/HMAC hoje. Proteger o endpoint receptor (allowlist de IP, token secreto na própria
/// URL, mTLS) é responsabilidade do parceiro.
/// </summary>
public static class AlmztechWebhook
{
    /// <summary>
    /// Classifica o payload cru recebido: <c>notas</c> presente e não vazio → Emissão; senão
    /// <c>dadosCancelamento</c> presente → Cancelamento; senão <c>status == "REJEITADO"</c> →
    /// Rejeição; senão lança <see cref="AlmztechWebhookParsingException"/>.
    /// </summary>
    public static EventoWebhook Parse(string corpoJsonCru)
    {
        if (string.IsNullOrWhiteSpace(corpoJsonCru))
            throw new AlmztechWebhookParsingException("Corpo do webhook vazio.");

        ProtocoloResponse? resposta;
        try
        {
            resposta = JsonSerializer.Deserialize<ProtocoloResponse>(corpoJsonCru, AlmztechHttpTransport.JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new AlmztechWebhookParsingException("Corpo do webhook não é um JSON válido.", ex);
        }

        if (resposta is null)
            throw new AlmztechWebhookParsingException("Corpo do webhook desserializou para nulo.");

        if (resposta.Notas is { Count: > 0 })
            return new EventoWebhook(TipoEventoWebhook.Emissao, resposta);

        if (resposta.DadosCancelamento is not null)
            return new EventoWebhook(TipoEventoWebhook.Cancelamento, resposta);

        if (resposta.Status == StatusProtocolo.REJEITADO)
            return new EventoWebhook(TipoEventoWebhook.Rejeicao, resposta);

        throw new AlmztechWebhookParsingException(
            $"Payload de webhook não reconhecido (protocolo='{resposta.Protocolo}', status='{resposta.Status}') " +
            "- não tem 'notas' nem 'dadosCancelamento', e o status não é REJEITADO.");
    }
}
