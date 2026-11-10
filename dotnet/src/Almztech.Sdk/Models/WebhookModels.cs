namespace Almztech.Sdk.Models;

/// <summary>
/// Serializado como número (0/1/2), não string — confirmado empiricamente batendo em
/// <c>POST /Emitente/Webhook/Teste</c> com <c>{"evento":0}</c>. Não decorar com
/// <c>JsonStringEnumConverter</c>.
/// </summary>
public enum WebhookEvento
{
    Emissao = 0,
    Cancelamento = 1,
    Rejeicao = 2,
}

/// <summary>Um registro de webhook = um evento. Para múltiplos eventos, cadastre múltiplos registros.</summary>
public class DtoWebhook
{
    public Guid? Codigo { get; set; }
    public WebhookEvento Evento { get; set; }
    public bool Ativo { get; set; } = true;
    public string Url { get; set; } = default!;
    public int CodigoEmitente { get; set; }
}

/// <summary>Corpo de <c>POST /Emitente/Webhook/Teste</c> — dispara um payload mockado sem precisar de webhook cadastrado.</summary>
public class DtoWebhookTeste
{
    public string Url { get; set; } = default!;
    public WebhookEvento Evento { get; set; }
}
