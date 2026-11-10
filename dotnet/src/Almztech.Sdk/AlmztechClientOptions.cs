namespace Almztech.Sdk;

public class AlmztechClientOptions
{
    /// <summary>Default de produção. Não existe endpoint de HML separado — ambiente é controlado por chamada (<c>enviarParaProducao</c>).</summary>
    public const string BaseUrlPadrao = "https://api.almztech.com.br";

    public string BaseUrl { get; set; } = BaseUrlPadrao;

    /// <summary>Timeout de rede por chamada. Não confundir com o tempo de processamento fiscal assíncrono — ver <c>NotasFiscaisClient.AguardarConclusaoAsync</c>.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public RetryOptions RetryOptions { get; set; } = RetryOptions.Default;

    /// <summary>
    /// Permite plugar um <see cref="HttpClient"/> próprio (proxy corporativo, handler customizado,
    /// instrumentação). Quando informado, <see cref="BaseUrl"/> e <see cref="Timeout"/> são
    /// ignorados — configure-os no client injetado.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>Hook opcional de log — sempre recebe payloads já sanitizados, ver <c>Logging.LogSanitizer</c>.</summary>
    public Action<string>? Logger { get; set; }
}
