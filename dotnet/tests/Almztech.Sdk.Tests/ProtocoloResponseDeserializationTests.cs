using System.Text.Json;
using FluentAssertions;
using Almztech.Sdk.Http;
using Almztech.Sdk.Models;

namespace Almztech.Sdk.Tests;

/// <summary>
/// Contract tests: garantem que os modelos do SDK desserializam corretamente os fixtures em
/// fixtures/ (respostas reais da API, com dados de teste — ver DESIGN.md
/// seção 0). Se a API mudar o formato real, estes testes devem quebrar antes que o parceiro sinta.
/// </summary>
[TestClass]
public class ProtocoloResponseDeserializationTests
{
    private static string CaminhoFixture(params string[] partes)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", Path.Combine(partes));

    private static T Desserializar<T>(string caminho)
    {
        var json = File.ReadAllText(caminho);
        return JsonSerializer.Deserialize<T>(json, AlmztechHttpTransport.JsonOptions)!;
    }

    [TestMethod]
    public void EmissaoComSucesso_DeveDesserializarNotasEStatusTerminal()
    {
        var caminho = CaminhoFixture("protocolo-responses", "emissao-sucesso.json");
        var resposta = Desserializar<ProtocoloResponse>(caminho);

        resposta.Protocolo.Should().Be("SBOX-af74e9db-5e30-41bf-a23b-fcf44beee899");
        resposta.Status.Should().Be(StatusProtocolo.PROCESSADO_COM_SUCESSO);
        resposta.DadosCancelamento.Should().BeNull();
        resposta.Notas.Should().HaveCount(1);
        resposta.Notas![0].CodigoNota.Should().Be(1);
        resposta.Notas[0].Situacao.Should().Be(StatusNota.EMITIDA);
        resposta.Notas[0].CodigoVerificacao.Should().Be("7AFNVLZL");
        resposta.Notas[0].ChaveAcesso.Should().Be("26116042269624110000120000000000099992610000000001");
    }

    [TestMethod]
    public void EmFila_DeveDesserializarSemNotasNemDadosCancelamento()
    {
        var resposta = Desserializar<ProtocoloResponse>(CaminhoFixture("protocolo-responses", "em-fila.json"));

        resposta.Status.Should().Be(StatusProtocolo.EM_FILA);
        resposta.Notas.Should().BeNull();
        resposta.DadosCancelamento.Should().BeNull();
    }

    [TestMethod]
    public void Rejeitado_DeveTrazerMensagemDeFalha()
    {
        var resposta = Desserializar<ProtocoloResponse>(CaminhoFixture("protocolo-responses", "rejeitado.json"));

        resposta.Status.Should().Be(StatusProtocolo.REJEITADO);
        resposta.Mensagem.Should().NotBeNullOrWhiteSpace();
        resposta.Notas.Should().BeNull();
    }

    [TestMethod]
    public void CancelamentoComSucesso_DeveDesserializarDadosCancelamento()
    {
        var resposta = Desserializar<ProtocoloResponse>(CaminhoFixture("protocolo-responses", "cancelamento-sucesso.json"));

        resposta.Status.Should().Be(StatusProtocolo.PROCESSADO_COM_SUCESSO);
        resposta.DadosCancelamento.Should().NotBeNull();
        resposta.DadosCancelamento!.CodigoNota.Should().Be(3);
        resposta.Notas.Should().BeNull();
    }

    [TestMethod]
    public void ListaDeNotas_DeveDesserializarStatusEValores()
    {
        var json = File.ReadAllText(CaminhoFixture("nota-fiscal-resumida", "lista-notas.json"));
        using var doc = JsonDocument.Parse(json);
        var respostaJson = doc.RootElement.GetProperty("response").GetRawText();

        var notas = JsonSerializer.Deserialize<List<DtoNotaFiscal>>(respostaJson, AlmztechHttpTransport.JsonOptions)!;

        notas.Should().HaveCount(2);
        notas[0].Status.Should().Be(StatusNota.PENDENTE_PREFEITURA);
        notas[1].Status.Should().Be(StatusNota.CANCELADA);
        notas[0].Protocolo.Should().StartWith("SBOX-");
    }

    [TestMethod]
    public void ErroDeValidacao_MensagemDeveSerExtraivelDoCorpo()
    {
        var json = File.ReadAllText(CaminhoFixture("erros", "422-validacao.json"));
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("httpStatus").GetInt32().Should().Be(422);
        doc.RootElement.GetProperty("body").GetProperty("mensagem").GetString()
            .Should().Contain("não encontrada");
    }
}
