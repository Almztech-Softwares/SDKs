namespace Almztech.Sdk.Models;

/// <summary>
/// Corpo de <c>POST /NFSe/ImportarPlanilha</c>. Prefira <c>NotasFiscaisClient.ImportarPlanilhaAsync</c>,
/// que aceita bytes/stream/caminho do <c>.xlsx</c> e converte para Base64 internamente.
/// </summary>
public class DtoImportacaoPlanilha
{
    public string ArquivoBase64 { get; set; } = default!;
    public string IdentificacaoEmitente { get; set; } = default!;
    public bool EnviarParaProducao { get; set; }
    public string? UrlWebhook { get; set; }
}

/// <summary>
/// Resultado da importação: um protocolo por lote gerado (acompanhe cada um como o de <c>EmitirAsync</c>) e
/// os erros por linha. Linhas com erro não são emitidas; as válidas seguem mesmo assim.
/// </summary>
public class ResultadoImportacaoPlanilha
{
    public List<ProtocoloResponse> Protocolos { get; set; } = new();
    public int TotalLinhasLidas { get; set; }
    public int TotalNotasValidas { get; set; }
    public List<ErroLinhaPlanilha> Erros { get; set; } = new();
}

public class ErroLinhaPlanilha
{
    /// <summary>Linha da planilha em que o erro foi encontrado.</summary>
    public int Linha { get; set; }

    public string Mensagem { get; set; } = default!;
}
