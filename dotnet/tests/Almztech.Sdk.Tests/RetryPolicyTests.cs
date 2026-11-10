using System.Net;
using FluentAssertions;
using Almztech.Sdk.Exceptions;
using Almztech.Sdk.Models;

namespace Almztech.Sdk.Tests;

/// <summary>
/// Cobre a regra central de DESIGN.md seção 4: leituras (GET/ConsultarComFiltros) retentam
/// automaticamente em 5xx/falha de rede; Emitir/Cancelar nunca retentam automaticamente, mesmo
/// configurados para tal, porque a chamada passa <c>permiteRetry: false</c> internamente.
/// </summary>
[TestClass]
public class RetryPolicyTests
{
    private static readonly RetryOptions RetryRapido = new()
    {
        MaxAttempts = 3,
        IntervaloInicial = TimeSpan.FromMilliseconds(1),
        IntervaloMaximo = TimeSpan.FromMilliseconds(5),
    };

    private static AlmztechClient CriarClient(FakeHttpMessageHandler handler, RetryOptions? retryOptions = null)
        => new("api-key-de-teste", new AlmztechClientOptions
        {
            HttpClient = FakeHttpMessageHandler.CriarHttpClient(handler),
            RetryOptions = retryOptions ?? RetryRapido,
        });

    [TestMethod]
    public async Task Get_DeveRetentarEmErro5xxEEntaoSucesso()
    {
        var protocoloJson = """{ "protocolo": "SBOX-1", "status": "EM_FILA", "mensagem": "" }""";
        var handler = new FakeHttpMessageHandler()
            .Enfileirar(HttpStatusCode.InternalServerError)
            .Enfileirar(HttpStatusCode.InternalServerError)
            .Enfileirar(HttpStatusCode.OK, protocoloJson);
        using var client = CriarClient(handler);

        var resposta = await client.NotasFiscais.ConsultarProtocoloAsync("SBOX-1");

        resposta.Status.Should().Be(StatusProtocolo.EM_FILA);
        handler.ChamadasRecebidas.Should().Be(3);
    }

    [TestMethod]
    public async Task Get_DeveRetentarEmFalhaDeRedeEEntaoSucesso()
    {
        var protocoloJson = """{ "protocolo": "SBOX-1", "status": "EM_FILA", "mensagem": "" }""";
        var handler = new FakeHttpMessageHandler()
            .EnfileirarFalhaDeRede()
            .Enfileirar(HttpStatusCode.OK, protocoloJson);
        using var client = CriarClient(handler);

        var resposta = await client.NotasFiscais.ConsultarProtocoloAsync("SBOX-1");

        resposta.Status.Should().Be(StatusProtocolo.EM_FILA);
        handler.ChamadasRecebidas.Should().Be(2);
    }

    [TestMethod]
    public async Task Get_DeveEsgotarTentativasELancarServerException()
    {
        var handler = new FakeHttpMessageHandler()
            .Enfileirar(HttpStatusCode.InternalServerError, """{"mensagem":"erro interno"}""")
            .Enfileirar(HttpStatusCode.InternalServerError, """{"mensagem":"erro interno"}""")
            .Enfileirar(HttpStatusCode.InternalServerError, """{"mensagem":"erro interno"}""");
        using var client = CriarClient(handler);

        var act = async () => await client.NotasFiscais.ConsultarProtocoloAsync("SBOX-1");

        await act.Should().ThrowAsync<AlmztechServerException>();
        handler.ChamadasRecebidas.Should().Be(3);
    }

    [TestMethod]
    public async Task Get_NuncaRetentaEm4xx()
    {
        var handler = new FakeHttpMessageHandler()
            .Enfileirar(HttpStatusCode.UnprocessableEntity, """{"mensagem":"Nota de código [999999] não encontrada."}""");
        using var client = CriarClient(handler);

        var act = async () => await client.NotasFiscais.ConsultarPorCodigoAsync(999999);

        (await act.Should().ThrowAsync<AlmztechValidationException>()).Which.Mensagem.Should().Contain("não encontrada");
        handler.ChamadasRecebidas.Should().Be(1);
    }

    [TestMethod]
    public async Task Emitir_NuncaRetentaMesmoEmErro5xx()
    {
        var handler = new FakeHttpMessageHandler()
            .Enfileirar(HttpStatusCode.InternalServerError, """{"mensagem":"erro interno"}""");
        using var client = CriarClient(handler);

        var lote = new RecepcaoLote { IdentificacaoEmitente = "00000000000000" };
        var act = async () => await client.NotasFiscais.EmitirAsync(lote);

        await act.Should().ThrowAsync<AlmztechServerException>();
        handler.ChamadasRecebidas.Should().Be(1, "Emitir nunca deve retentar automaticamente - risco de duplicar emissão fiscal");
    }

    [TestMethod]
    public async Task Cancelar_NuncaRetentaMesmoEmErro5xx()
    {
        var handler = new FakeHttpMessageHandler()
            .Enfileirar(HttpStatusCode.InternalServerError, """{"mensagem":"erro interno"}""");
        using var client = CriarClient(handler);

        var act = async () => await client.NotasFiscais.CancelarAsync(1);

        await act.Should().ThrowAsync<AlmztechServerException>();
        handler.ChamadasRecebidas.Should().Be(1);
    }

    [TestMethod]
    public async Task RespostaComStatus401_DeveLancarAuthenticationException()
    {
        var handler = new FakeHttpMessageHandler()
            .Enfileirar(HttpStatusCode.Unauthorized, """{"mensagem":"Token não informado"}""");
        using var client = CriarClient(handler);

        var act = async () => await client.NotasFiscais.ConsultarListaAsync("HML");

        (await act.Should().ThrowAsync<AlmztechAuthenticationException>()).Which.Mensagem.Should().Be("Token não informado");
    }

    [TestMethod]
    public async Task RespostaComStatus403_DeveLancarAuthorizationException()
    {
        var handler = new FakeHttpMessageHandler()
            .Enfileirar(HttpStatusCode.Forbidden, """{"mensagem":"Usuário não tem permissão."}""");
        using var client = CriarClient(handler);

        var act = async () => await client.Emitentes.ConsultarAsync();

        await act.Should().ThrowAsync<AlmztechAuthorizationException>();
    }
}
