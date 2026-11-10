using System.Net;

namespace Almztech.Sdk.Exceptions;

/// <summary>Base de todas as exceções lançadas pelo SDK. Ver DESIGN.md seção 3.</summary>
public abstract class AlmztechException : Exception
{
    protected AlmztechException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Erro retornado pela API com corpo <c>{ "mensagem": "..." }</c>. A API não expõe um
/// <c>errorCode</c> estruturado — <see cref="Mensagem"/> é sempre a mensagem crua em texto livre.
/// </summary>
public class AlmztechApiException : AlmztechException
{
    public HttpStatusCode StatusCode { get; }
    public string Mensagem { get; }
    public string? CorpoBruto { get; }

    public AlmztechApiException(HttpStatusCode statusCode, string mensagem, string? corpoBruto = null)
        : base($"A API da Almztech retornou {(int)statusCode}: {mensagem}")
    {
        StatusCode = statusCode;
        Mensagem = mensagem;
        CorpoBruto = corpoBruto;
    }
}

/// <summary>
/// 422 — erro de negócio da API, o mais comum na prática (validação/regra de negócio, inclusive
/// franquia do plano esgotada). Também 400, quando o corpo nem chega a ser lido (JSON com tipo errado num campo):
/// nesse caso <see cref="AlmztechApiException.Mensagem"/> junta os erros por campo que a API devolveu.
/// </summary>
public class AlmztechValidationException : AlmztechApiException
{
    public AlmztechValidationException(string mensagem, string? corpoBruto = null, HttpStatusCode statusCode = HttpStatusCode.UnprocessableEntity)
        : base(statusCode, mensagem, corpoBruto)
    {
    }
}

/// <summary>
/// 401 — token ausente, inválido ou revogado (o titular revogou a api key no Portal) OU licença inválida
/// para emitir em produção. A API não diferencia estruturalmente as causas; só a
/// <see cref="AlmztechApiException.Mensagem"/> muda. Não tente heurística de parsing de string para
/// sub-classificar — trate como uma única categoria.
/// </summary>
public class AlmztechAuthenticationException : AlmztechApiException
{
    public AlmztechAuthenticationException(string mensagem, string? corpoBruto = null)
        : base(HttpStatusCode.Unauthorized, mensagem, corpoBruto)
    {
    }
}

/// <summary>403 — papel de usuário sem permissão para o endpoint (ex. <c>ClienteFinal</c> em endpoint restrito).</summary>
public class AlmztechAuthorizationException : AlmztechApiException
{
    public AlmztechAuthorizationException(string mensagem, string? corpoBruto = null)
        : base(HttpStatusCode.Forbidden, mensagem, corpoBruto)
    {
    }
}

/// <summary>
/// 429 — limite de requisições excedido (por usuário e por IP, com limite próprio para <c>NFSe/Emitir</c>).
/// Diferente da franquia de notas do plano, que é <see cref="AlmztechValidationException"/>. O SDK não
/// retenta 429 sozinho: <see cref="RetryAfter"/> diz quanto esperar (a API informa a janela inteira, conservadora).
/// </summary>
public class AlmztechRateLimitException : AlmztechApiException
{
    public TimeSpan? RetryAfter { get; }

    public AlmztechRateLimitException(string mensagem, TimeSpan? retryAfter, string? corpoBruto = null)
        : base(HttpStatusCode.TooManyRequests, mensagem, corpoBruto)
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>500 — erro interno. Em produção, a mensagem pode vir genérica para usuários não-Operador.</summary>
public class AlmztechServerException : AlmztechApiException
{
    public AlmztechServerException(string mensagem, string? corpoBruto = null)
        : base(HttpStatusCode.InternalServerError, mensagem, corpoBruto)
    {
    }
}

/// <summary>Falha de rede/timeout/DNS antes de qualquer resposta HTTP ser recebida.</summary>
public class AlmztechNetworkException : AlmztechException
{
    public AlmztechNetworkException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// O helper <c>AguardarConclusao</c> estourou o timeout total configurado antes do protocolo
/// atingir um status terminal.
/// </summary>
public class AlmztechPollingTimeoutException : AlmztechException
{
    /// <summary>Último <c>ProtocoloResponse</c> conhecido antes do timeout — permite continuar o polling manualmente.</summary>
    public Models.ProtocoloResponse? UltimoProtocoloConhecido { get; }

    public AlmztechPollingTimeoutException(string protocolo, Models.ProtocoloResponse? ultimoProtocoloConhecido)
        : base($"Timeout aguardando conclusão do protocolo '{protocolo}'. Último status conhecido: {ultimoProtocoloConhecido?.Status.ToString() ?? "desconhecido"}.")
    {
        UltimoProtocoloConhecido = ultimoProtocoloConhecido;
    }
}

/// <summary>
/// O corpo recebido no endpoint de webhook do parceiro não corresponde a nenhum dos 3 formatos
/// conhecidos (Emissão/Cancelamento/Rejeição) — ver DESIGN.md seção 5.
/// </summary>
public class AlmztechWebhookParsingException : AlmztechException
{
    public AlmztechWebhookParsingException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// O protocolo atingiu um status terminal de falha (<c>REJEITADO</c> ou <c>ERRO_INTERNO</c>) durante
/// o polling de <c>AguardarConclusao</c>. Carrega a mensagem de falha da própria API.
/// </summary>
public class AlmztechProtocoloRejeitadoException : AlmztechException
{
    public Models.ProtocoloResponse Protocolo { get; }

    public AlmztechProtocoloRejeitadoException(Models.ProtocoloResponse protocolo)
        : base($"Protocolo '{protocolo.Protocolo}' terminou com status {protocolo.Status}: {protocolo.Mensagem}")
    {
        Protocolo = protocolo;
    }
}
