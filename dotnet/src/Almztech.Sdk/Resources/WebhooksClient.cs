using Almztech.Sdk.Http;
using Almztech.Sdk.Models;

namespace Almztech.Sdk.Resources;

/// <summary>
/// CRUD e teste de webhook (rota <c>Emitente/Webhook*</c>,
/// exposto aqui como client irmão de <see cref="EmitentesClient"/> — ver DESIGN.md seção 1).
/// Um registro de webhook = um evento (<see cref="WebhookEvento"/>); para múltiplos eventos,
/// cadastre múltiplos registros para o mesmo emitente.
/// </summary>
public class WebhooksClient
{
    private readonly AlmztechHttpTransport _transport;

    internal WebhooksClient(AlmztechHttpTransport transport)
    {
        _transport = transport;
    }

    /// <summary><c>GET /Emitente/Webhook/{codigo}</c>. Leitura — retry automático seguro.</summary>
    public Task<DtoWebhook> ObterAsync(Guid codigo, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<DtoWebhook>(HttpMethod.Get, $"Emitente/Webhook/{codigo}", null, permiteRetry: true, cancellationToken);

    /// <summary><c>GET /Emitente/Webhooks/{codigoEmitente}</c>. Leitura — retry automático seguro.</summary>
    public Task<List<DtoWebhook>> ListarPorEmitenteAsync(int codigoEmitente, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<List<DtoWebhook>>(HttpMethod.Get, $"Emitente/Webhooks/{codigoEmitente}", null, permiteRetry: true, cancellationToken);

    /// <summary><c>POST /Emitente/Webhook</c>. Sem retry automático (mutação — um retry cego duplicaria o registro; a API não valida duplicidade por URL).</summary>
    public Task<DtoWebhook> CriarAsync(DtoWebhook webhook, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<DtoWebhook>(HttpMethod.Post, "Emitente/Webhook", webhook, permiteRetry: false, cancellationToken);

    /// <summary><c>PUT /Emitente/Webhook</c>. Sem retry automático (mutação, baixo volume).</summary>
    public Task<DtoWebhook> AtualizarAsync(DtoWebhook webhook, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<DtoWebhook>(HttpMethod.Put, "Emitente/Webhook", webhook, permiteRetry: false, cancellationToken);

    /// <summary><c>DELETE /Emitente/Webhook/{codigo}</c>. Sem retry automático.</summary>
    public Task<MensagemResponse> ExcluirAsync(Guid codigo, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<MensagemResponse>(HttpMethod.Delete, $"Emitente/Webhook/{codigo}", null, permiteRetry: false, cancellationToken);

    /// <summary>
    /// <c>POST /Emitente/Webhook/Teste</c>. Dispara um payload mockado do evento informado para a
    /// <paramref name="teste"/>.Url, sem precisar de um webhook cadastrado — útil para o parceiro
    /// validar o endpoint receptor dele antes (ou depois) de cadastrar.
    /// </summary>
    public Task<MensagemResponse> TestarAsync(DtoWebhookTeste teste, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<MensagemResponse>(HttpMethod.Post, "Emitente/Webhook/Teste", teste, permiteRetry: false, cancellationToken);
}
