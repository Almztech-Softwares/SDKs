using System.Net;
using System.Text;

namespace Almztech.Sdk.Tests;

/// <summary>Handler HTTP fake para testar a política de retry e o mapeamento de exceções sem rede real.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _respostas = new();

    public int ChamadasRecebidas { get; private set; }
    public List<string> Caminhos { get; } = new();

    /// <summary>Corpo de cada request recebido (null quando não houve corpo), na ordem das chamadas.</summary>
    public List<string?> Corpos { get; } = new();

    public FakeHttpMessageHandler Enfileirar(HttpStatusCode status, string corpo = "")
    {
        _respostas.Enqueue(() => new HttpResponseMessage(status)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        });
        return this;
    }

    public FakeHttpMessageHandler EnfileirarComRetryAfter(HttpStatusCode status, string corpo, TimeSpan retryAfter)
    {
        _respostas.Enqueue(() =>
        {
            var resposta = new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
            resposta.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter);
            return resposta;
        });
        return this;
    }

    public FakeHttpMessageHandler EnfileirarFalhaDeRede()
    {
        _respostas.Enqueue(() => throw new HttpRequestException("Falha de rede simulada."));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ChamadasRecebidas++;
        Caminhos.Add(request.RequestUri!.PathAndQuery);
        Corpos.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

        if (_respostas.Count == 0)
            throw new InvalidOperationException("FakeHttpMessageHandler sem respostas enfileiradas suficientes.");

        var gerarResposta = _respostas.Dequeue();
        return gerarResposta();
    }

    public static HttpClient CriarHttpClient(FakeHttpMessageHandler handler)
        => new(handler) { BaseAddress = new Uri("https://sdk-tests.local/") };
}
