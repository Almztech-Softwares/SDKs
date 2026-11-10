using Almztech.Sdk.Http;
using Almztech.Sdk.Models;

namespace Almztech.Sdk.Resources;

/// <summary>CRUD de Emitente e upload de certificado A1 (rota <c>Emitente/*</c>).</summary>
public class EmitentesClient
{
    private readonly AlmztechHttpTransport _transport;

    internal EmitentesClient(AlmztechHttpTransport transport)
    {
        _transport = transport;
    }

    /// <summary><c>GET /Emitente/Consultar</c>. Até 50 emitentes do usuário autenticado. Leitura — retry automático seguro.</summary>
    public Task<List<DtoEmitente>> ConsultarAsync(CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<List<DtoEmitente>>(HttpMethod.Get, "Emitente/Consultar", null, permiteRetry: true, cancellationToken);

    /// <summary><c>GET /Emitente/Consultar/{codigo}</c>. Leitura — retry automático seguro.</summary>
    public Task<DtoEmitente> ConsultarPorCodigoAsync(int codigo, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<DtoEmitente>(HttpMethod.Get, $"Emitente/Consultar/{codigo}", null, permiteRetry: true, cancellationToken);

    /// <summary><c>GET /Emitente/ConsultarCpfCnpj/{cpfCnpj}</c>. Leitura — retry automático seguro.</summary>
    public Task<DtoEmitente> ConsultarPorCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<DtoEmitente>(HttpMethod.Get, $"Emitente/ConsultarCpfCnpj/{Uri.EscapeDataString(cpfCnpj)}", null, permiteRetry: true, cancellationToken);

    /// <summary><c>GET /Emitente/ConsultaPorTermo/{termo}</c>. Busca livre. Leitura — retry automático seguro.</summary>
    public Task<List<DtoEmitente>> ConsultarPorTermoAsync(string termo, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<List<DtoEmitente>>(HttpMethod.Get, $"Emitente/ConsultaPorTermo/{Uri.EscapeDataString(termo)}", null, permiteRetry: true, cancellationToken);

    /// <summary><c>POST /Emitente/Cadastrar</c>. Devolve o código do emitente criado. Sem retry automático (mutação).</summary>
    public Task<ResultadoGravacaoEmitente> CadastrarAsync(DtoEmitente emitente, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<ResultadoGravacaoEmitente>(HttpMethod.Post, "Emitente/Cadastrar", emitente, permiteRetry: false, cancellationToken);

    /// <summary>
    /// <c>PUT /Emitente/Atualizar</c>. Grava todos os campos do corpo, inclusive os padrões fiscais: parta do
    /// emitente devolvido por <see cref="ConsultarPorCodigoAsync"/> (ver <see cref="DtoEmitente"/>). Sem retry automático (mutação).
    /// </summary>
    public Task<ResultadoGravacaoEmitente> AtualizarAsync(DtoEmitente emitente, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<ResultadoGravacaoEmitente>(HttpMethod.Put, "Emitente/Atualizar", emitente, permiteRetry: false, cancellationToken);

    /// <summary><c>DELETE /Emitente/Excluir/{codigo}</c>. Sem retry automático.</summary>
    public Task<MensagemResponse> ExcluirAsync(int codigo, CancellationToken cancellationToken = default)
        => _transport.EnviarAsync<MensagemResponse>(HttpMethod.Delete, $"Emitente/Excluir/{codigo}", null, permiteRetry: false, cancellationToken);

    /// <summary>
    /// <c>POST /Emitente/UploadCertificado</c>. Converte os bytes do <c>.pfx</c> para Base64
    /// internamente — o parceiro nunca precisa lidar com o encoding manual. Devolve a chave e o
    /// vencimento do certificado. Sem retry automático (mutação sensível).
    /// </summary>
    public Task<ResultadoUploadCertificado> UploadCertificadoAsync(
        string cnpjEmissor, byte[] certificadoPfx, string senha, CancellationToken cancellationToken = default)
    {
        var corpo = new DtoUploadCertificado
        {
            CnpjEmissor = cnpjEmissor,
            Base64 = Convert.ToBase64String(certificadoPfx),
            Senha = senha,
        };

        return _transport.EnviarAsync<ResultadoUploadCertificado>(HttpMethod.Post, "Emitente/UploadCertificado", corpo, permiteRetry: false, cancellationToken);
    }

    /// <summary>Overload de <see cref="UploadCertificadoAsync(string,byte[],string,CancellationToken)"/> lendo o <c>.pfx</c> de um <see cref="Stream"/>.</summary>
    public async Task<ResultadoUploadCertificado> UploadCertificadoAsync(
        string cnpjEmissor, Stream certificadoPfx, string senha, CancellationToken cancellationToken = default)
    {
        using var memoria = new MemoryStream();
        await certificadoPfx.CopyToAsync(memoria, cancellationToken).ConfigureAwait(false);
        return await UploadCertificadoAsync(cnpjEmissor, memoria.ToArray(), senha, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Overload de <see cref="UploadCertificadoAsync(string,byte[],string,CancellationToken)"/> lendo o <c>.pfx</c> direto de um caminho de arquivo.</summary>
    public async Task<ResultadoUploadCertificado> UploadCertificadoAsync(
        string cnpjEmissor, string caminhoArquivoPfx, string senha, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(caminhoArquivoPfx, cancellationToken).ConfigureAwait(false);
        return await UploadCertificadoAsync(cnpjEmissor, bytes, senha, cancellationToken).ConfigureAwait(false);
    }
}
