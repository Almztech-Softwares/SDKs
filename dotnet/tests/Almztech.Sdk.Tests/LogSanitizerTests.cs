using FluentAssertions;
using Almztech.Sdk.Logging;

namespace Almztech.Sdk.Tests;

[TestClass]
public class LogSanitizerTests
{
    [TestMethod]
    public void SanitizarApiKey_DeveMostrarSoUltimosQuatroCaracteres()
    {
        LogSanitizer.SanitizarApiKey("N7azRlAkAI6BkXdWoXcEf4wpMhRjpSzCxkAWBPNu5QhMj+qqAerdXg==")
            .Should().Be("***Xg==");
    }

    [TestMethod]
    public void SanitizarCorpoJson_DeveMascararCamposSensiveisEmQualquerProfundidade()
    {
        var corpo = """
        {
          "razaoSocial": "Batista S.A.",
          "senhaCertificado": "sandbox",
          "senhaPrefeitura": "sandbox",
          "endereco": { "logradouro": "Rua X", "senha": "nao-deveria-aparecer" }
        }
        """;

        var sanitizado = LogSanitizer.SanitizarCorpoJson(corpo);

        sanitizado.Should().Contain("Batista S.A.");
        sanitizado.Should().Contain("Rua X");
        sanitizado.Should().NotContain("sandbox");
        sanitizado.Should().NotContain("nao-deveria-aparecer");
    }

    [TestMethod]
    public void SanitizarCorpoJson_DeveMascararBase64DeCertificadoEmArray()
    {
        var corpo = """{ "itens": [{ "cnpjEmissor": "123", "base64": "MUITO-LONGO-BASE64" }] }""";

        var sanitizado = LogSanitizer.SanitizarCorpoJson(corpo);

        sanitizado.Should().NotContain("MUITO-LONGO-BASE64");
        sanitizado.Should().Contain("123");
    }

    [TestMethod]
    public void SanitizarCorpoJson_CorpoNaoJson_NaoVazaConteudoOriginal()
    {
        var sanitizado = LogSanitizer.SanitizarCorpoJson("<html>não é json</html>");

        sanitizado.Should().NotContain("não é json");
    }

    [TestMethod]
    public void SanitizarCorpoJson_CorpoVazio_RetornaVazio()
    {
        LogSanitizer.SanitizarCorpoJson("").Should().BeEmpty();
        LogSanitizer.SanitizarCorpoJson(null).Should().BeEmpty();
    }
}
