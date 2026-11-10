using System.Net;
using System.Text.Json;
using FluentAssertions;
using Almztech.Sdk.Exceptions;
using Almztech.Sdk.Logging;
using Almztech.Sdk.Models;

namespace Almztech.Sdk.Tests;

/// <summary>
/// Wire format conferido contra a API atual: tipos que ela aceita, nulos omitidos e os formatos de resposta
/// que não são o próprio DTO (cadastro de emitente, upload de certificado, municípios).
/// </summary>
[TestClass]
public class ContratoDaApiTests
{
    private static AlmztechClient CriarClient(FakeHttpMessageHandler handler)
        => new("api-key-de-teste", new AlmztechClientOptions
        {
            HttpClient = FakeHttpMessageHandler.CriarHttpClient(handler),
            RetryOptions = new RetryOptions { MaxAttempts = 3, IntervaloInicial = TimeSpan.FromMilliseconds(1), IntervaloMaximo = TimeSpan.FromMilliseconds(5) },
        });

    private const string ProtocoloEmFila = """{ "protocolo": "HML-1", "status": "EM_FILA" }""";

    [TestMethod]
    public async Task Emitir_DeveMandarCodigosFiscaisComoNumeroEOmitirNulos()
    {
        var handler = new FakeHttpMessageHandler().Enfileirar(HttpStatusCode.OK, ProtocoloEmFila);
        using var client = CriarClient(handler);

        await client.NotasFiscais.EmitirAsync(new RecepcaoLote
        {
            IdentificacaoEmitente = "12345678000190",
            Notas =
            [
                new NotaFiscalResumida
                {
                    Rps = new IdentificacaoDaNota { Serie = "1", Tipo = "1", DataEmissao = new DateOnly(2026, 10, 31) },
                    Destinatario = new Destinatario { CpfCnpj = "12345678909", RazaoSocial = "Cliente", Endereco = new Endereco { CodigoIBGEMunicipio = 2611606 } },
                    Servicos = [new Servico { Codigo = "0105", Descricao = "Teste", ValorUnitario = 100m, RetemISS = 2, ExigibilidadeISS = 1, AliquotaISS = 2.5m }],
                    SubtituicaoRps = new SubtituicaoRps { ChaveNota = "NFS123", CodigoMotivo = "01" },
                },
            ],
        });

        using var corpo = JsonDocument.Parse(handler.Corpos.Single()!);
        var nota = corpo.RootElement.GetProperty("notas")[0];
        var servico = nota.GetProperty("servicos")[0];

        nota.GetProperty("rps").GetProperty("tipo").GetString().Should().Be("1");
        nota.GetProperty("rps").GetProperty("dataEmissao").GetString().Should().Be("2026-10-31");
        nota.GetProperty("rps").TryGetProperty("numeroRPS", out _).Should().BeFalse("nulo é omitido e a API numera o RPS");
        servico.GetProperty("retemISS").GetInt32().Should().Be(2);
        servico.GetProperty("exigibilidadeISS").GetInt32().Should().Be(1);
        servico.TryGetProperty("tipoTributacao", out _).Should().BeFalse("int nulo viraria 400 na API");
        nota.GetProperty("subtituicaoRps").GetProperty("chaveNota").GetString().Should().Be("NFS123");
        nota.TryGetProperty("observacao", out _).Should().BeFalse();
    }

    [TestMethod]
    public async Task Municipios_DeveLerOIdNumericoEMandarOCodigoIbgeNaQuery()
    {
        var handler = new FakeHttpMessageHandler().Enfileirar(HttpStatusCode.OK, """[{ "id": 2611606, "text": "2611606 - Recife" }]""");
        using var client = CriarClient(handler);

        var municipios = await client.Municipios.HomologadosAsync(codigoIbge: 2611606);

        municipios.Should().ContainSingle().Which.Id.Should().Be(2611606);
        handler.Caminhos.Single().Should().Be("/CoberturaNacional/MunicipiosHomologados?codigo=2611606");
    }

    [TestMethod]
    public async Task CadastrarEmitente_DeveDevolverOCodigoCriado()
    {
        var handler = new FakeHttpMessageHandler().Enfileirar(HttpStatusCode.OK,
            """{ "mensagem": "Emitente cadastrado com sucesso.", "cpfCnpj": "12345678000190", "codigo": 42 }""");
        using var client = CriarClient(handler);

        var resultado = await client.Emitentes.CadastrarAsync(new DtoEmitente { RazaoSocial = "Empresa", CpfCnpj = "12345678000190" });

        resultado.Codigo.Should().Be(42);
        resultado.Mensagem.Should().Contain("cadastrado");
    }

    [TestMethod]
    public async Task UploadCertificado_DeveMandarBase64EDevolverChaveEVencimento()
    {
        var handler = new FakeHttpMessageHandler().Enfileirar(HttpStatusCode.OK,
            """{ "mensagem": "ok", "chaveCertificado": "abc.pfx", "vencimento": "2027-05-01T00:00:00", "aviso": null }""");
        using var client = CriarClient(handler);

        var resultado = await client.Emitentes.UploadCertificadoAsync("12345678000190", [1, 2, 3], "senha");

        resultado.ChaveCertificado.Should().Be("abc.pfx");
        resultado.Vencimento.Should().Be(new DateTime(2027, 5, 1));
        JsonDocument.Parse(handler.Corpos.Single()!).RootElement.GetProperty("base64").GetString().Should().Be("AQID");
    }

    [TestMethod]
    public async Task ImportarPlanilha_DeveMandarOArquivoEmBase64ENaoRetentar()
    {
        var handler = new FakeHttpMessageHandler().Enfileirar(HttpStatusCode.InternalServerError, """{ "mensagem": "falhou" }""");
        using var client = CriarClient(handler);

        var importar = () => client.NotasFiscais.ImportarPlanilhaAsync("12345678000190", [1, 2, 3], enviarParaProducao: false);

        await importar.Should().ThrowAsync<AlmztechServerException>();
        handler.ChamadasRecebidas.Should().Be(1, "importar emite notas e não pode ser repetido às cegas");
        JsonDocument.Parse(handler.Corpos.Single()!).RootElement.GetProperty("arquivoBase64").GetString().Should().Be("AQID");
    }

    [TestMethod]
    public async Task ImportarPlanilha_DeveLerProtocolosEErrosPorLinha()
    {
        var handler = new FakeHttpMessageHandler().Enfileirar(HttpStatusCode.OK, """
            { "protocolos": [{ "protocolo": "HML-9", "status": "EM_FILA" }], "totalLinhasLidas": 3, "totalNotasValidas": 2,
              "erros": [{ "linha": 4, "mensagem": "CPF/CNPJ do tomador inválido" }] }
            """);
        using var client = CriarClient(handler);

        var resultado = await client.NotasFiscais.ImportarPlanilhaAsync("12345678000190", [1], enviarParaProducao: false);

        resultado.Protocolos.Should().ContainSingle().Which.Status.Should().Be(StatusProtocolo.EM_FILA);
        resultado.Erros.Should().ContainSingle().Which.Linha.Should().Be(4);
    }

    [TestMethod]
    public async Task RateLimit_DeveVirarExcecaoComRetryAfterSemRetentar()
    {
        var handler = new FakeHttpMessageHandler()
            .EnfileirarComRetryAfter((HttpStatusCode)429, """{ "mensagem": "Limite de requisições excedido. Tente novamente em 60 segundo(s)." }""", TimeSpan.FromSeconds(60));
        using var client = CriarClient(handler);

        var consultar = () => client.NotasFiscais.ConsultarProtocoloAsync("HML-1");

        var excecao = (await consultar.Should().ThrowAsync<AlmztechRateLimitException>()).Which;
        excecao.RetryAfter.Should().Be(TimeSpan.FromSeconds(60));
        handler.ChamadasRecebidas.Should().Be(1, "retentar antes do Retry-After só renderia outro 429");
    }

    [TestMethod]
    public async Task BadRequestDoAspNet_DeveVirarValidacaoComOsErrosPorCampo()
    {
        var handler = new FakeHttpMessageHandler().Enfileirar(HttpStatusCode.BadRequest, """
            { "title": "One or more validation errors occurred.", "status": 400,
              "errors": { "notas[0].servicos[0].retemISS": ["Could not convert string to integer: true."] } }
            """);
        using var client = CriarClient(handler);

        var emitir = () => client.NotasFiscais.EmitirAsync(new RecepcaoLote { IdentificacaoEmitente = "1" });

        var excecao = (await emitir.Should().ThrowAsync<AlmztechValidationException>()).Which;
        excecao.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        excecao.Mensagem.Should().Contain("retemISS");
    }

    [TestMethod]
    public async Task ConsultarComFiltros_DeveMandarValoresComoTextoELerAsContagensPorAba()
    {
        var handler = new FakeHttpMessageHandler().Enfileirar(HttpStatusCode.OK, """
            { "notas": [{ "codigoNota": 7, "numeroNota": "123", "chaveAcesso": "NFS999", "status": "EMITIDA", "protocolo": "PRD-1" }],
              "paginaAtual": 1, "totalDeRegistros": 1, "totalEmitidas": 1,
              "contagensPorAba": { "all": 5, "EMITIDA": 1, "CANCELADA": 4 } }
            """);
        using var client = CriarClient(handler);

        var resultado = await client.NotasFiscais.ConsultarComFiltrosAsync(new DtoParametrosFiltros
        {
            Filtros =
            [
                DtoFiltro.Periodo(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)),
                DtoFiltro.ComStatus(StatusNota.EMITIDA, StatusNota.CANCELADA),
                DtoFiltro.EntreValores(CamposFiltro.ValorTotal, 10.5m, 100m),
            ],
        });

        resultado.Notas.Single().NumeroNota.Should().Be("123");
        resultado.Notas.Single().ChaveAcesso.Should().Be("NFS999");
        resultado.ContagensPorAba["CANCELADA"].Should().Be(4);

        var filtros = JsonDocument.Parse(handler.Corpos.Single()!).RootElement;
        filtros.GetProperty("ambiente").GetString().Should().Be(Ambiente.Producao);
        var lista = filtros.GetProperty("filtros");
        lista[0].GetProperty("valor").GetString().Should().Be("2026-10-01");
        lista[0].GetProperty("valorFinal").GetString().Should().Be("2026-10-31");
        lista[0].GetProperty("operador").GetString().Should().Be("Entre");
        lista[1].GetProperty("valor").GetString().Should().Be("EMITIDA|CANCELADA");
        lista[2].GetProperty("valor").GetString().Should().Be("10.5");
    }

    [TestMethod]
    public async Task ReconsultarPorChave_DeveMandarOAmbienteNoCorpo()
    {
        var handler = new FakeHttpMessageHandler().Enfileirar(HttpStatusCode.OK,
            """{ "codigoNota": 7, "situacao": "CANCELADA", "dataEmissao": "2026-10-01", "protocolo": "PRD-1" }""");
        using var client = CriarClient(handler);

        var nota = await client.NotasFiscais.ReconsultarPorChaveAcessoAsync("NFS26116", enviarParaProducao: true);

        nota.Situacao.Should().Be(StatusNota.CANCELADA);
        handler.Caminhos.Single().Should().Be("/NFSe/Reconsultar/ChaveAcesso/NFS26116");
        JsonDocument.Parse(handler.Corpos.Single()!).RootElement.GetProperty("enviarParaProducao").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public void LogSanitizer_DeveMascararAPlanilhaImportada()
    {
        var sanitizado = LogSanitizer.SanitizarCorpoJson("""{ "arquivoBase64": "UEsDBBQ=", "identificacaoEmitente": "123" }""");

        sanitizado.Should().NotContain("UEsDBBQ=");
        sanitizado.Should().Contain("123");
    }
}
