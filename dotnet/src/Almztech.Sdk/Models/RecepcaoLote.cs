namespace Almztech.Sdk.Models;

/// <summary>
/// Corpo de <c>POST /NFSe/Emitir</c>.
/// Propriedades nulas não são enviadas (a API aplica o padrão dela) — campo numérico da API recebendo
/// <c>null</c> faria o request ser recusado.
/// </summary>
public class RecepcaoLote
{
    /// <summary>CPF/CNPJ do emitente cadastrado (ver <c>EmitentesClient</c>).</summary>
    public string IdentificacaoEmitente { get; set; } = default!;
    public List<NotaFiscalResumida> Notas { get; set; } = new();

    /// <summary>
    /// <c>false</c> gera protocolo de homologação (prefixo <c>HML-</c>); <c>true</c> gera protocolo
    /// de produção (prefixo <c>PRD-</c>). Não confundir com ambiente do client — é por lote.
    /// Homologação não consome a franquia do plano; produção exige licença válida e franquia disponível.
    /// </summary>
    public bool EnviarParaProducao { get; set; }

    /// <summary>Webhook só deste lote, além dos cadastrados para o emitente.</summary>
    public string? UrlWebhook { get; set; }
}

public class NotaFiscalResumida
{
    public IdentificacaoDaNota Rps { get; set; } = new();
    public Destinatario Destinatario { get; set; } = new();
    public List<Servico> Servicos { get; set; } = new();
    public List<CondicaoPagamento>? CondicoesPagamentos { get; set; }
    public string? Observacao { get; set; }

    /// <summary>
    /// Nome preserva intencionalmente o typo existente no schema real da API
    /// (<c>Subtituicao</c>, sem o "s" de "Substituição") — não "corrigir" aqui, quebraria o wire format.
    /// </summary>
    public SubtituicaoRps? SubtituicaoRps { get; set; }
}

public class IdentificacaoDaNota
{
    /// <summary>Opcional quando o emitente tem controle automático de RPS: a API numera.</summary>
    public string? NumeroRPS { get; set; }
    public string? NumeroNota { get; set; }
    public string? Serie { get; set; }

    /// <summary>Tipo do RPS (ex.: <c>"1"</c> = RPS). Sem valor, vale o padrão do emitente.</summary>
    public string? Tipo { get; set; }

    /// <summary>Situação do RPS: 1 = normal (padrão da API), 2 = cancelado.</summary>
    public int? Status { get; set; }

    public DateOnly? DataEmissao { get; set; }
    public DateOnly? DataCompetencia { get; set; }
    public string? CodigoVerificacao { get; set; }
    public string? ChaveNota { get; set; }
}

public class Destinatario
{
    public string CpfCnpj { get; set; } = default!;
    public string RazaoSocial { get; set; } = default!;
    public string? NomeFantasia { get; set; }
    public string? InscricaoEstadual { get; set; }
    public string? InscricaoMunicipal { get; set; }
    public string? Telefone { get; set; }

    /// <summary>
    /// Precisa do <see cref="Endereco.CodigoIBGEMunicipio"/>: sem ele a nota é aceita no envio e falha
    /// depois, no processamento assíncrono.
    /// </summary>
    public Endereco? Endereco { get; set; }
}

public class Endereco
{
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cep { get; set; }

    /// <summary>Código BACEN do país. A API assume 1058 (Brasil) quando omitido.</summary>
    public int? CodigoPais { get; set; }

    public int? CodigoIBGEUF { get; set; }
    public int? CodigoIBGEMunicipio { get; set; }
    public string? SiglaUF { get; set; }
    public string? DescricaoUF { get; set; }
    public string? DescricaoMunicipio { get; set; }
}

/// <summary>
/// Um item de serviço da nota. Os códigos fiscais são números, como na API; os omitidos (nulos) usam
/// o padrão da API, e os padrões fiscais do emitente só são aplicados na importação de planilha.
/// </summary>
public class Servico
{
    public string Codigo { get; set; } = default!;
    public string Descricao { get; set; } = default!;
    public string? CodigoTributacao { get; set; }

    /// <summary>Código IBGE (7 dígitos) do município onde o serviço foi prestado.</summary>
    public string? LocalPrestacao { get; set; }

    /// <summary>Responsável pela retenção (prestador ou tomador), conforme a tabela da prefeitura de destino.</summary>
    public int? ResponsavelPelaRetencao { get; set; }

    /// <summary>
    /// Tabela ABRASF: 1 = Exigível, 2 = Não incidência, 3 = Isenção, 4 = Exportação, 5 = Imunidade,
    /// 6 = Suspensa por decisão judicial, 7 = Suspensa por processo administrativo.
    /// </summary>
    public int? ExigibilidadeISS { get; set; }

    /// <summary>Tipo de tributação do ISS; os valores válidos variam conforme o município de prestação.</summary>
    public int? TipoTributacao { get; set; }

    /// <summary>Código IBGE (7 dígitos) do município onde o ISS incide.</summary>
    public int? MunicipioIncidencia { get; set; }

    public decimal? AliquotaISS { get; set; }

    /// <summary>1 = ISS retido pelo tomador, 2 = não retido.</summary>
    public int? RetemISS { get; set; }

    public decimal Quantidade { get; set; } = 1;
    public decimal ValorUnitario { get; set; }
    public decimal? ValorDesconto { get; set; }
    public decimal? ValorAcrescimo { get; set; }
    public string? CodigoCnae { get; set; }
    public string? NumeroProcesso { get; set; }
    public string? Nbs { get; set; }

    /// <summary>Grupo IBS/CBS da Reforma Tributária (leiaute nacional).</summary>
    public IbsCbs? IbsCbs { get; set; }

    public PisCofins? PisCofins { get; set; }
    public TotaisDosTributos? TotaisDosTributos { get; set; }
}

public class IbsCbs
{
    public string? CodigoOperacao { get; set; }
    public string? OperacaoTrabalhista { get; set; }
    public string? CategoriaOperacao { get; set; }
    public string? CategoriaEntePublico { get; set; }
    public string? DescricaoCategoriaEntePublico { get; set; }
    public RegimeIbsCbs? Regime { get; set; }
}

public class RegimeIbsCbs
{
    /// <summary>CST do IBS/CBS.</summary>
    public string? Cst { get; set; }

    /// <summary>Código de classificação tributária.</summary>
    public string? Cct { get; set; }
}

public class PisCofins
{
    public string? ModalidadeRetencao { get; set; }
    public decimal? BaseDeCalculo { get; set; }
    public string? Cst { get; set; }
    public DetalhesPisCofins? Pis { get; set; }
    public DetalhesPisCofins? Cofins { get; set; }
}

public class DetalhesPisCofins
{
    public decimal? Percentual { get; set; }
    public decimal? Valor { get; set; }
}

/// <summary>Percentuais aproximados dos tributos (Lei da Transparência), quando o leiaute pede.</summary>
public class TotaisDosTributos
{
    public decimal? PercentualDosTributosFederais { get; set; }
    public decimal? PercentualDosTributosEstaduais { get; set; }
    public decimal? PercentualDosTributosMunicipais { get; set; }
    public decimal? PercentualDosTributosDoSimplesNacional { get; set; }
}

public class CondicaoPagamento
{
    public string? Condicao { get; set; }
    public List<Pagamento>? Pagamentos { get; set; }
}

public class Pagamento
{
    public int Parcela { get; set; }
    public DateOnly? DataPagamento { get; set; }
    public decimal Valor { get; set; }
}

/// <summary>Identificação do RPS/nota substituída por esta (nome com o typo do schema real, ver <see cref="NotaFiscalResumida.SubtituicaoRps"/>).</summary>
public class SubtituicaoRps
{
    public string? NumeroRPS { get; set; }
    public string? NumeroNota { get; set; }
    public string? Serie { get; set; }
    public string? Tipo { get; set; }

    /// <summary>Chave de acesso da nota substituída (Emissor Nacional).</summary>
    public string? ChaveNota { get; set; }

    public string? CodigoMotivo { get; set; }
    public string? Motivo { get; set; }
}
