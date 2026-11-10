using Almztech.Sdk.Http;
using Almztech.Sdk.Resources;

namespace Almztech.Sdk;

/// <summary>
/// Client raiz do SDK .NET da Almztech. Expõe um sub-client por recurso — ver DESIGN.md
/// seção 1. Thread-safe para reuso: crie uma instância por aplicação, não por request.
/// </summary>
/// <example>
/// <code>
/// using var client = new AlmztechClient(apiKey);
/// var protocolo = await client.NotasFiscais.EmitirAsync(lote);
/// var resultado = await client.NotasFiscais.AguardarConclusaoAsync(protocolo.Protocolo);
/// </code>
/// </example>
public sealed class AlmztechClient : IDisposable
{
    private readonly AlmztechHttpTransport _transport;

    /// <summary>Emissão, consulta, cancelamento e download de NFS-e.</summary>
    public NotasFiscaisClient NotasFiscais { get; }

    /// <summary>CRUD e teste de webhook.</summary>
    public WebhooksClient Webhooks { get; }

    /// <summary>CRUD de Emitente e upload de certificado A1.</summary>
    public EmitentesClient Emitentes { get; }

    /// <summary>Consulta de municípios homologados.</summary>
    public MunicipiosClient Municipios { get; }

    /// <summary>
    /// A obtenção da <paramref name="apiKey"/> (login/senha → <c>GET /Usuario/ApiKey</c>) é
    /// deliberadamente um passo de setup fora deste client — ver DESIGN.md seção 2. Não guarde
    /// login/senha no código do parceiro; guarde só a apiKey resultante.
    /// </summary>
    public AlmztechClient(string apiKey, AlmztechClientOptions? options = null)
    {
        options ??= new AlmztechClientOptions();
        _transport = new AlmztechHttpTransport(apiKey, options);

        NotasFiscais = new NotasFiscaisClient(_transport);
        Webhooks = new WebhooksClient(_transport);
        Emitentes = new EmitentesClient(_transport);
        Municipios = new MunicipiosClient(_transport);
    }

    public void Dispose() => _transport.Dispose();
}
