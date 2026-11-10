using System.Net;
using FluentAssertions;
using Almztech.Sdk.Exceptions;
using Almztech.Sdk.Models;

namespace Almztech.Sdk.Tests;

[TestClass]
public class PollingTests
{
    private static AlmztechClient CriarClient(FakeHttpMessageHandler handler)
        => new("api-key-de-teste", new AlmztechClientOptions
        {
            HttpClient = FakeHttpMessageHandler.CriarHttpClient(handler),
        });

    private static readonly AguardarConclusaoOptions OpcoesRapidas = new()
    {
        IntervaloInicial = TimeSpan.FromMilliseconds(1),
        IntervaloMaximo = TimeSpan.FromMilliseconds(5),
        TimeoutTotal = TimeSpan.FromSeconds(2),
    };

    [TestMethod]
    public async Task AguardarConclusao_DeveContinuarPollingAteStatusTerminalDeSucesso()
    {
        var handler = new FakeHttpMessageHandler()
            .Enfileirar(HttpStatusCode.OK, """{ "protocolo": "SBOX-1", "status": "EM_FILA", "mensagem": "" }""")
            .Enfileirar(HttpStatusCode.OK, """{ "protocolo": "SBOX-1", "status": "EM_PROCESSAMENTO", "mensagem": "" }""")
            .Enfileirar(HttpStatusCode.OK, """{ "protocolo": "SBOX-1", "status": "PROCESSADO_COM_SUCESSO", "mensagem": "", "notas": [{"codigoNota":1,"rps":"1","serie":"1","dataEmissao":"2026-08-04","situacao":"EMITIDA","protocolo":"SBOX-1"}] }""");
        using var client = CriarClient(handler);
        var tentativas = new List<StatusProtocolo>();

        var resultado = await client.NotasFiscais.AguardarConclusaoAsync(
            "SBOX-1",
            new AguardarConclusaoOptions
            {
                IntervaloInicial = OpcoesRapidas.IntervaloInicial,
                IntervaloMaximo = OpcoesRapidas.IntervaloMaximo,
                TimeoutTotal = OpcoesRapidas.TimeoutTotal,
                OnTentativa = p => tentativas.Add(p.Status),
            });

        resultado.Status.Should().Be(StatusProtocolo.PROCESSADO_COM_SUCESSO);
        handler.ChamadasRecebidas.Should().Be(3);
        tentativas.Should().Equal(StatusProtocolo.EM_FILA, StatusProtocolo.EM_PROCESSAMENTO, StatusProtocolo.PROCESSADO_COM_SUCESSO);
    }

    [TestMethod]
    public async Task AguardarConclusao_StatusRejeitado_DeveLancarProtocoloRejeitadoException()
    {
        var handler = new FakeHttpMessageHandler()
            .Enfileirar(HttpStatusCode.OK, """{ "protocolo": "SBOX-1", "status": "REJEITADO", "mensagem": "CNPJ inválido" }""");
        using var client = CriarClient(handler);

        var act = async () => await client.NotasFiscais.AguardarConclusaoAsync("SBOX-1", OpcoesRapidas);

        var excecao = (await act.Should().ThrowAsync<AlmztechProtocoloRejeitadoException>()).Which;
        excecao.Protocolo.Mensagem.Should().Be("CNPJ inválido");
    }

    [TestMethod]
    public async Task AguardarConclusao_NuncaAtingeStatusTerminal_DeveLancarPollingTimeoutException()
    {
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < 50; i++)
            handler.Enfileirar(HttpStatusCode.OK, """{ "protocolo": "SBOX-1", "status": "PENDENTE_PREFEITURA", "mensagem": "" }""");
        using var client = CriarClient(handler);

        var act = async () => await client.NotasFiscais.AguardarConclusaoAsync("SBOX-1", new AguardarConclusaoOptions
        {
            IntervaloInicial = TimeSpan.FromMilliseconds(1),
            IntervaloMaximo = TimeSpan.FromMilliseconds(2),
            TimeoutTotal = TimeSpan.FromMilliseconds(50),
        });

        var excecao = (await act.Should().ThrowAsync<AlmztechPollingTimeoutException>()).Which;
        excecao.UltimoProtocoloConhecido.Should().NotBeNull();
        excecao.UltimoProtocoloConhecido!.Status.Should().Be(StatusProtocolo.PENDENTE_PREFEITURA);
    }
}
