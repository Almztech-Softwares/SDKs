using Almztech.Sdk.Exceptions;
using Almztech.Sdk.Http;
using Almztech.Sdk.Models;

namespace Almztech.Sdk.Resources;

/// <summary>
/// Emissão, consulta, cancelamento e download de NFS-e (rota
/// <c>NFSe/*</c>). Ver DESIGN.md seções 1 e 4.
/// </summary>
public class NotasFiscaisClient
{
    private readonly AlmztechHttpTransport _transport;

    internal NotasFiscaisClient(AlmztechHttpTransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// <c>POST /NFSe/Emitir</c>. Assíncrono no lado da API — o retorno é só o protocolo em fila,
    /// use <see cref="AguardarConclusaoAsync"/> ou o webhook para acompanhar a conclusão.
    /// Em produção exige licença válida (senão <see cref="AlmztechAuthenticationException"/>) e franquia
    /// disponível no plano (senão <see cref="AlmztechValidationException"/>); homologação não consome
    /// franquia. Tem limite de requisições próprio (<see cref="AlmztechRateLimitException"/>).
    /// Sem retry automático por padrão (evita duplicar emissão fiscal) — ver DESIGN.md seção 4.
    /// </summary>
    public Task<ProtocoloResponse> EmitirAsync(RecepcaoLote lote, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<ProtocoloResponse>(HttpMethod.Post, "NFSe/Emitir", lote, permiteRetry: false, cancellationToken);

    /// <summary>
    /// Helper de polling de alto nível: consulta <see cref="ConsultarProtocoloAsync"/> repetidamente
    /// (backoff exponencial) até o protocolo atingir um status terminal, ou lança
    /// <see cref="AlmztechPollingTimeoutException"/> se estourar <see cref="AguardarConclusaoOptions.TimeoutTotal"/>.
    /// Estados de falha (<c>REJEITADO</c>/<c>ERRO_INTERNO</c>) lançam <see cref="AlmztechProtocoloRejeitadoException"/>
    /// em vez de retornar silenciosamente. Complementar ao webhook, não substituto.
    /// </summary>
    public async Task<ProtocoloResponse> AguardarConclusaoAsync(
        string protocolo, AguardarConclusaoOptions? opcoes = null, CancellationToken cancellationToken = default)
    {
        opcoes ??= new AguardarConclusaoOptions();
        var inicio = DateTime.UtcNow;
        var atraso = opcoes.IntervaloInicial;
        ProtocoloResponse? ultimoConhecido = null;

        while (true)
        {
            var resposta = await ConsultarProtocoloAsync(protocolo, cancellationToken).ConfigureAwait(false);
            ultimoConhecido = resposta;
            opcoes.OnTentativa?.Invoke(resposta);

            switch (resposta.Status)
            {
                case StatusProtocolo.PROCESSADO_COM_SUCESSO:
                case StatusProtocolo.CANCELADO:
                    return resposta;
                case StatusProtocolo.REJEITADO:
                case StatusProtocolo.ERRO_INTERNO:
                    throw new AlmztechProtocoloRejeitadoException(resposta);
            }

            if (DateTime.UtcNow - inicio >= opcoes.TimeoutTotal)
                throw new AlmztechPollingTimeoutException(protocolo, ultimoConhecido);

            await Task.Delay(atraso, cancellationToken).ConfigureAwait(false);
            var proximoAtrasoMs = Math.Min(atraso.TotalMilliseconds * opcoes.BackoffMultiplicador, opcoes.IntervaloMaximo.TotalMilliseconds);
            atraso = TimeSpan.FromMilliseconds(proximoAtrasoMs);
        }
    }

    /// <summary><c>GET /NFSe/Consultar/Protocolo/{protocolo}</c>. Leitura — retry automático seguro.</summary>
    public Task<ProtocoloResponse> ConsultarProtocoloAsync(string protocolo, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<ProtocoloResponse>(
            HttpMethod.Get, $"NFSe/Consultar/Protocolo/{Uri.EscapeDataString(protocolo)}", null, permiteRetry: true, cancellationToken);

    /// <summary><c>GET /NFSe/Consultar/{codigoNota}</c>. Leitura — retry automático seguro.</summary>
    public Task<ProtocoloNotaResponse> ConsultarPorCodigoAsync(int codigoNota, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<ProtocoloNotaResponse>(
            HttpMethod.Get, $"NFSe/Consultar/{codigoNota}", null, permiteRetry: true, cancellationToken);

    /// <summary>
    /// <c>GET /NFSe/Consultar/Numero/{numeroNota}</c>: nota pelo número da NFS-e na prefeitura. Havendo
    /// mais de uma nota com o mesmo número entre os seus emitentes, vem a mais recente. Leitura — retry automático seguro.
    /// </summary>
    public Task<ProtocoloNotaResponse> ConsultarPorNumeroAsync(string numeroNota, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<ProtocoloNotaResponse>(
            HttpMethod.Get, $"NFSe/Consultar/Numero/{Uri.EscapeDataString(numeroNota)}", null, permiteRetry: true, cancellationToken);

    /// <summary>
    /// <c>GET /NFSe/Reconsultar/{codigoNota}</c>: consulta a nota de novo na prefeitura e atualiza a situação
    /// local (ex.: nota pendente ou cancelada direto no portal da prefeitura). Repetir é seguro — retry automático.
    /// </summary>
    public Task<ProtocoloNotaResponse> ReconsultarAsync(int codigoNota, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<ProtocoloNotaResponse>(
            HttpMethod.Get, $"NFSe/Reconsultar/{codigoNota}", null, permiteRetry: true, cancellationToken);

    /// <summary>
    /// <c>POST /NFSe/Reconsultar/ChaveAcesso/{chaveAcesso}</c>: reconsulta pela chave de acesso do Emissor
    /// Nacional (o CNPJ do emitente sai da própria chave) e atualiza a nota local com o mesmo número.
    /// Repetir é seguro — retry automático.
    /// </summary>
    public Task<ProtocoloNotaResponse> ReconsultarPorChaveAcessoAsync(
        string chaveAcesso, bool enviarParaProducao, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<ProtocoloNotaResponse>(
            HttpMethod.Post, $"NFSe/Reconsultar/ChaveAcesso/{Uri.EscapeDataString(chaveAcesso)}",
            new { enviarParaProducao }, permiteRetry: true, cancellationToken);

    /// <summary><c>GET /NFSe/ConsultarLista/{ambiente}</c> (<see cref="Ambiente"/>). Até 50 notas, as mais recentes. Leitura — retry automático seguro.</summary>
    public Task<List<DtoNotaFiscal>> ConsultarListaAsync(string ambiente, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<List<DtoNotaFiscal>>(
            HttpMethod.Get, $"NFSe/ConsultarLista/{Uri.EscapeDataString(ambiente)}", null, permiteRetry: true, cancellationToken);

    /// <summary><c>GET /NFSe/ConsultarListaPorTermo/{ambiente}/{termo}</c>. Busca por CNPJ, código, número, razão social, nome fantasia ou protocolo. Leitura — retry automático seguro.</summary>
    public Task<List<DtoNotaFiscal>> ConsultarListaPorTermoAsync(string ambiente, string termo, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<List<DtoNotaFiscal>>(
            HttpMethod.Get,
            $"NFSe/ConsultarListaPorTermo/{Uri.EscapeDataString(ambiente)}/{Uri.EscapeDataString(termo)}",
            null, permiteRetry: true, cancellationToken);

    /// <summary>
    /// <c>POST /NFSe/ConsultarComFiltros</c>. É um POST por causa do corpo de filtros complexo, mas
    /// semanticamente é leitura (sem efeito colateral) — retry automático seguro.
    /// </summary>
    public Task<ResultadoConsultaComFiltros> ConsultarComFiltrosAsync(
        DtoParametrosFiltros parametros, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<ResultadoConsultaComFiltros>(
            HttpMethod.Post, "NFSe/ConsultarComFiltros", parametros, permiteRetry: true, cancellationToken);

    /// <summary>
    /// Itera automaticamente todas as páginas de <see cref="ConsultarComFiltrosAsync"/>, poupando o
    /// loop manual de paginação. Respeita <see cref="DtoParametrosFiltros.LimiteDaPagina"/> (limite
    /// de 100 por página aplicado pela API).
    /// </summary>
    public async IAsyncEnumerable<DtoNotaFiscal> ConsultarComFiltrosPaginasAsync(
        DtoParametrosFiltros parametros,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pagina = parametros.Pagina < 1 ? 1 : parametros.Pagina;

        while (true)
        {
            var pedido = new DtoParametrosFiltros
            {
                Ambiente = parametros.Ambiente,
                FiltroRapido = parametros.FiltroRapido,
                Filtros = parametros.Filtros,
                LimiteDaPagina = parametros.LimiteDaPagina,
                Pagina = pagina,
            };

            var resultado = await ConsultarComFiltrosAsync(pedido, cancellationToken).ConfigureAwait(false);

            if (resultado.Notas.Count == 0)
                yield break;

            foreach (var nota in resultado.Notas)
                yield return nota;

            var totalDePaginas = (int)Math.Ceiling(resultado.TotalDeRegistros / (double)Math.Max(1, pedido.LimiteDaPagina));
            if (pagina >= totalDePaginas)
                yield break;

            pagina++;
        }
    }

    /// <summary>
    /// <c>POST /NFSe/Cancelar/{codigoNota}</c>. Assíncrono, use <see cref="AguardarConclusaoAsync"/>
    /// ou webhook para acompanhar. Sem retry automático por padrão (mesma cautela de <see cref="EmitirAsync"/>).
    /// </summary>
    public Task<ProtocoloResponse> CancelarAsync(
        int codigoNota, DtoCancelamentoNota? corpo = null, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<ProtocoloResponse>(
            HttpMethod.Post, $"NFSe/Cancelar/{codigoNota}", corpo ?? new DtoCancelamentoNota(), permiteRetry: false, cancellationToken);

    /// <summary><c>GET /NFSe/ModeloPlanilha</c>: bytes do <c>.xlsx</c> modelo de <see cref="ImportarPlanilhaAsync(string,byte[],bool,string?,CancellationToken)"/>.</summary>
    public Task<byte[]> BaixarModeloPlanilhaAsync(CancellationToken cancellationToken = default)
        => _transport.BaixarBytesAsync("NFSe/ModeloPlanilha", cancellationToken);

    /// <summary>
    /// <c>POST /NFSe/ImportarPlanilha</c>: emite as notas de uma planilha no formato do modelo
    /// (<see cref="BaixarModeloPlanilhaAsync"/>), aplicando os padrões fiscais do emitente. Converte o
    /// arquivo para Base64 internamente. Sem retry automático (emite notas, como <see cref="EmitirAsync"/>).
    /// </summary>
    public Task<ResultadoImportacaoPlanilha> ImportarPlanilhaAsync(
        string identificacaoEmitente, byte[] planilhaXlsx, bool enviarParaProducao, string? urlWebhook = null,
        CancellationToken cancellationToken = default)
    {
        var corpo = new DtoImportacaoPlanilha
        {
            ArquivoBase64 = Convert.ToBase64String(planilhaXlsx),
            IdentificacaoEmitente = identificacaoEmitente,
            EnviarParaProducao = enviarParaProducao,
            UrlWebhook = urlWebhook,
        };

        return _transport.EnviarAsync<ResultadoImportacaoPlanilha>(HttpMethod.Post, "NFSe/ImportarPlanilha", corpo, permiteRetry: false, cancellationToken);
    }

    /// <summary>Overload de <see cref="ImportarPlanilhaAsync(string,byte[],bool,string?,CancellationToken)"/> lendo o <c>.xlsx</c> de um <see cref="Stream"/>.</summary>
    public async Task<ResultadoImportacaoPlanilha> ImportarPlanilhaAsync(
        string identificacaoEmitente, Stream planilhaXlsx, bool enviarParaProducao, string? urlWebhook = null,
        CancellationToken cancellationToken = default)
    {
        using var memoria = new MemoryStream();
        await planilhaXlsx.CopyToAsync(memoria, cancellationToken).ConfigureAwait(false);
        return await ImportarPlanilhaAsync(identificacaoEmitente, memoria.ToArray(), enviarParaProducao, urlWebhook, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Overload de <see cref="ImportarPlanilhaAsync(string,byte[],bool,string?,CancellationToken)"/> lendo o <c>.xlsx</c> de um caminho de arquivo.</summary>
    public async Task<ResultadoImportacaoPlanilha> ImportarPlanilhaAsync(
        string identificacaoEmitente, string caminhoArquivoXlsx, bool enviarParaProducao, string? urlWebhook = null,
        CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(caminhoArquivoXlsx, cancellationToken).ConfigureAwait(false);
        return await ImportarPlanilhaAsync(identificacaoEmitente, bytes, enviarParaProducao, urlWebhook, cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>GET /NFSe/XML/{codigoNota}</c>. Retorna os bytes do XML — persistência em disco é responsabilidade do chamador.</summary>
    public Task<byte[]> BaixarXmlAsync(int codigoNota, CancellationToken cancellationToken = default)
        => _transport.BaixarBytesAsync($"NFSe/XML/{codigoNota}", cancellationToken);

    /// <summary><c>GET /NFSe/PDF/{codigoNota}</c>. Retorna os bytes do PDF — persistência em disco é responsabilidade do chamador.</summary>
    public Task<byte[]> BaixarPdfAsync(int codigoNota, CancellationToken cancellationToken = default)
        => _transport.BaixarBytesAsync($"NFSe/PDF/{codigoNota}", cancellationToken);
}
