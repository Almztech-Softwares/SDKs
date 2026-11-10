using System.Net;
using System.Text;
using System.Text.Json;
using Almztech.Sdk.Exceptions;
using Almztech.Sdk.Logging;

namespace Almztech.Sdk.Http;

/// <summary>
/// Encapsula o <see cref="HttpClient"/>: header <c>x-api-key</c>, User-Agent, política de retry
/// (ver <see cref="RetryOptions"/> e DESIGN.md seção 4) e mapeamento de respostas de erro
/// para a hierarquia de exceções tipadas. Não é exposto publicamente — os sub-clients de recurso
/// que o consomem decidem, por chamada, se a operação é segura para retry.
/// </summary>
internal sealed class AlmztechHttpTransport : IDisposable
{
    internal const string VersaoSdk = "0.2.0-beta.2";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // Campo numérico obrigatório da API recusa null com 400: omitir deixa valer o padrão dela.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly RetryOptions _retryOptions;
    private readonly Action<string>? _logger;

    public AlmztechHttpTransport(string apiKey, AlmztechClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("apiKey é obrigatório.", nameof(apiKey));

        _retryOptions = options.RetryOptions ?? RetryOptions.Default;
        _logger = options.Logger;

        if (options.HttpClient is not null)
        {
            _httpClient = options.HttpClient;
            _ownsHttpClient = false;
        }
        else
        {
            var baseUri = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            _httpClient = new HttpClient { BaseAddress = baseUri, Timeout = options.Timeout };
            _ownsHttpClient = true;
        }

        _httpClient.DefaultRequestHeaders.Remove("x-api-key");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"almztech-dotnet-sdk/{VersaoSdk}");

        _logger?.Invoke($"AlmztechClient configurado (apiKey={LogSanitizer.SanitizarApiKey(apiKey)}, baseUrl={_httpClient.BaseAddress})");
    }

    public async Task<TResponse> EnviarAsync<TResponse>(
        HttpMethod method, string path, object? corpo, bool permiteRetry, CancellationToken cancellationToken)
    {
        using var response = await EnviarComRetryAsync(method, path, corpo, permiteRetry, cancellationToken).ConfigureAwait(false);
        var conteudo = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _logger?.Invoke($"<- {(int)response.StatusCode} {method} {path} {LogSanitizer.SanitizarCorpoJson(conteudo)}");

        if (!response.IsSuccessStatusCode)
            throw MapearExcecao(response, conteudo);

        if (string.IsNullOrWhiteSpace(conteudo))
            return default!;

        return JsonSerializer.Deserialize<TResponse>(conteudo, JsonOptions)
               ?? throw new AlmztechApiException(response.StatusCode, "Resposta vazia inesperada da API.", conteudo);
    }

    public async Task<byte[]> BaixarBytesAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await EnviarComRetryAsync(HttpMethod.Get, path, null, permiteRetry: true, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var conteudo = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw MapearExcecao(response, conteudo);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> EnviarComRetryAsync(
        HttpMethod method, string path, object? corpo, bool permiteRetry, CancellationToken cancellationToken)
    {
        var tentativasMax = permiteRetry ? Math.Max(1, _retryOptions.MaxAttempts) : 1;
        Exception? ultimaFalhaDeRede = null;

        for (var tentativa = 1; tentativa <= tentativasMax; tentativa++)
        {
            try
            {
                using var request = new HttpRequestMessage(method, path);
                if (corpo is not null)
                {
                    var json = JsonSerializer.Serialize(corpo, JsonOptions);
                    _logger?.Invoke($"-> {method} {path} {LogSanitizer.SanitizarCorpoJson(json)}");
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                var deveTentarNovamente = permiteRetry && tentativa < tentativasMax && (int)response.StatusCode >= 500;
                if (deveTentarNovamente)
                {
                    response.Dispose();
                    await Task.Delay(_retryOptions.CalcularAtraso(tentativa), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return response;
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
            {
                ultimaFalhaDeRede = ex;
                if (!permiteRetry || tentativa >= tentativasMax)
                    throw new AlmztechNetworkException($"Falha de rede chamando {method} {path}: {ex.Message}", ex);

                await Task.Delay(_retryOptions.CalcularAtraso(tentativa), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new AlmztechNetworkException($"Falha de rede chamando {method} {path} após {tentativasMax} tentativa(s).", ultimaFalhaDeRede);
    }

    private static AlmztechApiException MapearExcecao(HttpResponseMessage response, string corpo)
    {
        var status = response.StatusCode;
        var mensagem = ExtrairMensagem(corpo);
        return status switch
        {
            HttpStatusCode.Unauthorized => new AlmztechAuthenticationException(mensagem, corpo),
            HttpStatusCode.Forbidden => new AlmztechAuthorizationException(mensagem, corpo),
            HttpStatusCode.UnprocessableEntity => new AlmztechValidationException(mensagem, corpo),
            HttpStatusCode.BadRequest => new AlmztechValidationException(mensagem, corpo, HttpStatusCode.BadRequest),
            HttpStatusCode.TooManyRequests => new AlmztechRateLimitException(mensagem, LerRetryAfter(response), corpo),
            >= HttpStatusCode.InternalServerError => new AlmztechServerException(mensagem, corpo),
            _ => new AlmztechApiException(status, mensagem, corpo),
        };
    }

    private static TimeSpan? LerRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } espera)
            return espera;

        if (retryAfter?.Date is { } data)
            return data - DateTimeOffset.UtcNow;

        return null;
    }

    /// <summary>
    /// <c>{ "mensagem" }</c> é o formato de toda resposta de erro da API. O 400 do ASP.NET (corpo que nem
    /// chegou a ser lido) vem como ProblemDetails, com os erros por campo em <c>errors</c>.
    /// </summary>
    private static string ExtrairMensagem(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
            return "(sem corpo de resposta)";

        try
        {
            using var doc = JsonDocument.Parse(corpo);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("mensagem", out var prop)
                && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? corpo;
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("errors", out var erros)
                && erros.ValueKind == JsonValueKind.Object)
            {
                var mensagens = erros.EnumerateObject()
                    .SelectMany(campo => campo.Value.ValueKind == JsonValueKind.Array
                        ? campo.Value.EnumerateArray().Select(erro => $"{campo.Name}: {erro.GetString()}")
                        : [$"{campo.Name}: {campo.Value}"]);
                return string.Join("; ", mensagens);
            }
        }
        catch (JsonException)
        {
            // corpo não é JSON - cai no fallback abaixo.
        }

        return corpo;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
