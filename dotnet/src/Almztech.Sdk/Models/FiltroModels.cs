using System.Globalization;
using System.Text.Json.Serialization;

namespace Almztech.Sdk.Models;

/// <summary>
/// Operadores aceitos por <c>DtoFiltro.Operador</c>. Serializado como string (nome do membro).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperadorFiltro
{
    Igual,
    Diferente,
    MaiorQue,
    MaiorOuIgual,
    MenorQue,
    MenorOuIgual,
    Entre,
    Contem,
    IniciaCom,
    TerminaCom,
}

/// <summary>
/// Campos aceitos por <see cref="DtoFiltro.Campo"/> — os que a API aceita em <c>NFSe/ConsultarComFiltros</c>.
/// Campo desconhecido ou valor vazio é ignorado pela API (não filtra), por isso use estas constantes.
/// </summary>
public static class CamposFiltro
{
    /// <summary>Texto: identificador do protocolo (<c>PRD-...</c>/<c>HML-...</c>).</summary>
    public const string Protocolo = "protocolo";

    /// <summary>Inteiro: código da nota na Almztech.</summary>
    public const string CodigoNota = "codigoNota";

    /// <summary>Texto: razão social do emitente.</summary>
    public const string NomeEmitente = "nomeEmitente";

    /// <summary>Texto: CPF/CNPJ do emitente, só dígitos.</summary>
    public const string CpfCnpjEmitente = "cpfCnpjEmitente";

    public const string NumeroRps = "numeroRps";
    public const string SerieRps = "serieRps";

    /// <summary>Inteiro: código do lote.</summary>
    public const string LoteRps = "loteRps";

    /// <summary>Decimal: valor dos serviços da nota.</summary>
    public const string ValorTotal = "valorTotal";

    /// <summary>Data (<c>yyyy-MM-dd</c>): data de emissão/autorização.</summary>
    public const string DataEmissao = "dataEmissao";

    /// <summary>Status da nota; vários separados por <c>|</c> (ex.: <c>EMITIDA|CANCELADA</c>). Só aceita <see cref="OperadorFiltro.Igual"/>.</summary>
    public const string Status = "status";
}

/// <summary>Um critério de <c>ConsultarComFiltros</c>. Prefira os construtores estáticos, que formatam o valor como a API espera.</summary>
public class DtoFiltro
{
    public string Campo { get; set; } = default!;
    public OperadorFiltro Operador { get; set; }

    /// <summary>Sempre texto no wire format; datas em <c>yyyy-MM-dd</c> e decimais com ponto.</summary>
    public string Valor { get; set; } = default!;

    /// <summary>Fim do intervalo, só com <see cref="OperadorFiltro.Entre"/>.</summary>
    public string? ValorFinal { get; set; }

    public static DtoFiltro Texto(string campo, OperadorFiltro operador, string valor)
        => new() { Campo = campo, Operador = operador, Valor = valor };

    public static DtoFiltro Numero(string campo, OperadorFiltro operador, decimal valor)
        => new() { Campo = campo, Operador = operador, Valor = valor.ToString(CultureInfo.InvariantCulture) };

    public static DtoFiltro EntreValores(string campo, decimal inicio, decimal fim)
        => new() { Campo = campo, Operador = OperadorFiltro.Entre, Valor = inicio.ToString(CultureInfo.InvariantCulture), ValorFinal = fim.ToString(CultureInfo.InvariantCulture) };

    public static DtoFiltro Data(OperadorFiltro operador, DateOnly data)
        => new() { Campo = CamposFiltro.DataEmissao, Operador = operador, Valor = data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };

    public static DtoFiltro Periodo(DateOnly inicio, DateOnly fim)
        => new()
        {
            Campo = CamposFiltro.DataEmissao,
            Operador = OperadorFiltro.Entre,
            Valor = inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ValorFinal = fim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

    public static DtoFiltro ComStatus(params StatusNota[] status)
        => new() { Campo = CamposFiltro.Status, Operador = OperadorFiltro.Igual, Valor = string.Join('|', status) };
}

/// <summary>Ambiente das consultas de lista: o prefixo do protocolo das notas.</summary>
public static class Ambiente
{
    public const string Producao = "PRD";
    public const string Homologacao = "HML";
}

/// <summary>Corpo de <c>POST /NFSe/ConsultarComFiltros</c>.</summary>
public class DtoParametrosFiltros
{
    /// <summary>Obrigatório: <see cref="Models.Ambiente.Producao"/> ou <see cref="Models.Ambiente.Homologacao"/>.</summary>
    public string Ambiente { get; set; } = Models.Ambiente.Producao;

    /// <summary>Busca livre por código, CPF/CNPJ do emitente, número da nota, número do RPS, nome fantasia ou protocolo.</summary>
    public string? FiltroRapido { get; set; }

    public List<DtoFiltro>? Filtros { get; set; }
    public int Pagina { get; set; } = 1;

    /// <summary>Limite de 100 por página; a API reduz pedidos maiores.</summary>
    public int LimiteDaPagina { get; set; } = 50;
}

public class ResultadoConsultaComFiltros
{
    public List<DtoNotaFiscal> Notas { get; set; } = new();
    public int PaginaAtual { get; set; }

    /// <summary>Total com todos os filtros, inclusive o de status.</summary>
    public int TotalDeRegistros { get; set; }

    public int TotalEmProcessamento { get; set; }
    public int TotalEmitidas { get; set; }
    public int TotalCanceladas { get; set; }
    public int TotalDenegadas { get; set; }
    public int TotalRejeitadas { get; set; }
    public int TotalPendentes { get; set; }

    /// <summary>
    /// Contagem por status ignorando o próprio filtro de status (chaves <c>all</c> e os nomes de
    /// <see cref="StatusNota"/>): mostra quantas notas cada status teria com os demais filtros.
    /// </summary>
    public Dictionary<string, int> ContagensPorAba { get; set; } = new();
}
