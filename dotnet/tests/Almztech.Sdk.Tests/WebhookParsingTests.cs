using FluentAssertions;
using Almztech.Sdk.Exceptions;
using Almztech.Sdk.Webhooks;

namespace Almztech.Sdk.Tests;

[TestClass]
public class WebhookParsingTests
{
    private static string LerFixture(string arquivo)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "webhook-payloads", arquivo));

    [TestMethod]
    public void Emissao_DeveClassificarPeloNotasPreenchido()
    {
        var evento = AlmztechWebhook.Parse(LerFixture("emissao.json"));

        evento.Tipo.Should().Be(TipoEventoWebhook.Emissao);
        evento.Notas.Should().HaveCount(1);
        evento.DadosCancelamento.Should().BeNull();
    }

    [TestMethod]
    public void Cancelamento_DeveClassificarPeloDadosCancelamentoPreenchido()
    {
        var evento = AlmztechWebhook.Parse(LerFixture("cancelamento.json"));

        evento.Tipo.Should().Be(TipoEventoWebhook.Cancelamento);
        evento.DadosCancelamento.Should().NotBeNull();
        evento.DadosCancelamento!.CodigoNota.Should().Be(3);
        evento.Notas.Should().BeEmpty();
    }

    [TestMethod]
    public void Rejeicao_DeveClassificarPeloStatusRejeitadoSemNotasOuCancelamento()
    {
        var evento = AlmztechWebhook.Parse(LerFixture("rejeicao.json"));

        evento.Tipo.Should().Be(TipoEventoWebhook.Rejeicao);
        evento.Mensagem.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public void PayloadSemFormatoReconhecido_DeveLancarWebhookParsingException()
    {
        var corpo = """{ "protocolo": "PRD-x", "status": "EM_FILA" }""";

        var act = () => AlmztechWebhook.Parse(corpo);

        act.Should().Throw<AlmztechWebhookParsingException>();
    }

    [TestMethod]
    public void JsonInvalido_DeveLancarWebhookParsingException()
    {
        var act = () => AlmztechWebhook.Parse("isso não é json");

        act.Should().Throw<AlmztechWebhookParsingException>();
    }

    [TestMethod]
    public void CorpoVazio_DeveLancarWebhookParsingException()
    {
        var act = () => AlmztechWebhook.Parse("");

        act.Should().Throw<AlmztechWebhookParsingException>();
    }
}
