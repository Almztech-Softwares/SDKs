namespace Almztech.Sdk.Models;

/// <summary>
/// Schema central retornado por <c>GET /NFSe/Consultar/Protocolo/{protocolo}</c>, por
/// <c>POST /NFSe/Emitir</c>/<c>POST /NFSe/Cancelar</c>, e é o mesmo formato recebido no
/// endpoint de webhook do parceiro. Campos ausentes na resposta real (não nulos, ausentes)
/// viram <c>null</c> aqui após a desserialização.
/// </summary>
public class ProtocoloResponse
{
    public string Protocolo { get; set; } = default!;
    public StatusProtocolo Status { get; set; }
    public string? Mensagem { get; set; }
    public List<ProtocoloNotaResponse>? Notas { get; set; }
    public ProtocoloCancelamentoResponse? DadosCancelamento { get; set; }
}

public class ProtocoloNotaResponse
{
    public int CodigoNota { get; set; }
    public string? TipoNota { get; set; }
    public string? Numero { get; set; }
    public string? Rps { get; set; }
    public string? Serie { get; set; }
    public DateOnly DataEmissao { get; set; }
    public StatusNota Situacao { get; set; }
    public string? CodigoVerificacao { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? Xml { get; set; }
    public string? Pdf { get; set; }
    public DateTime? DataHoraCancelamento { get; set; }
    public string Protocolo { get; set; } = default!;
}

public class ProtocoloCancelamentoResponse
{
    public int CodigoNota { get; set; }
    public string? NumeroNota { get; set; }
}
