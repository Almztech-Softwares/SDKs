namespace Almztech.Sdk.Models;

/// <summary>Item retornado por <c>ConsultarLista</c>/<c>ConsultarListaPorTermo</c>/<c>ConsultarComFiltros</c>.</summary>
public class DtoNotaFiscal
{
    public int CodigoNota { get; set; }

    /// <summary>Número da NFS-e na prefeitura; vazio enquanto a nota não foi autorizada.</summary>
    public string? NumeroNota { get; set; }

    public int CodigoEmitente { get; set; }
    public string? CpfCnpjEmitente { get; set; }
    public string? NomeEmitente { get; set; }
    public string? NumeroRPS { get; set; }
    public string? SerieRPS { get; set; }
    public int LoteRPS { get; set; }
    public decimal ValorTotal { get; set; }
    public StatusNota Status { get; set; }
    public DateOnly? DataAutorizacao { get; set; }
    public string? TempoDeProcessamento { get; set; }
    public string Protocolo { get; set; } = default!;

    /// <summary>Chave de acesso (Emissor Nacional), quando a nota tem uma.</summary>
    public string? ChaveAcesso { get; set; }
}

/// <summary>Corpo opcional de <c>POST /NFSe/Cancelar/{codigoNota}</c>.</summary>
public class DtoCancelamentoNota
{
    public string? UrlWebhook { get; set; }
}
