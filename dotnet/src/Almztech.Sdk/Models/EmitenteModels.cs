namespace Almztech.Sdk.Models;

/// <summary>
/// Schema de <c>Emitente/Cadastrar</c>, <c>Emitente/Atualizar</c> e das consultas de emitente.
/// Atenção: <see cref="SenhaCertificado"/> e <see cref="SenhaPrefeitura"/> vêm em texto puro
/// inclusive em respostas de leitura — nunca logar (ver <c>Logging.LogSanitizer</c>).
/// </summary>
/// <remarks>
/// <c>Atualizar</c> grava <b>todos</b> os campos do corpo, inclusive os padrões fiscais: atualize a partir
/// do emitente consultado, nunca de um objeto novo com só os campos alterados. As exceções, que mantêm o
/// valor gravado quando vêm vazias, são <see cref="SenhaCertificado"/>, <see cref="SenhaPrefeitura"/>,
/// <see cref="ChaveCertificado"/>, <see cref="VencimentoCertificado"/>, <see cref="ControleAutomaticoRPS"/>
/// e as sequências de RPS.
/// </remarks>
public class DtoEmitente
{
    public int? Codigo { get; set; }
    public string RazaoSocial { get; set; } = default!;
    public string? NomeFantasia { get; set; }
    public string CpfCnpj { get; set; } = default!;
    public string? InscricaoEstadual { get; set; }
    public string? InscricaoMunicipal { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }

    /// <summary>Preenchida pelo upload do certificado (<c>EmitentesClient.UploadCertificadoAsync</c>).</summary>
    public string? ChaveCertificado { get; set; }

    public string? SenhaCertificado { get; set; }
    public DateTime? VencimentoCertificado { get; set; }
    public string? UsuarioPrefeitura { get; set; }
    public string? SenhaPrefeitura { get; set; }
    public string? CodigoCnae { get; set; }
    public bool IncentivadorCultural { get; set; }
    public string? IncentivoFiscal { get; set; }
    public bool OptanteSimples { get; set; }
    public int RegimeTributacao { get; set; }

    /// <summary>Com o controle ligado, a API numera o RPS (o <c>NumeroRPS</c> da nota pode ficar vazio). Nulo na atualização mantém o valor atual.</summary>
    public bool? ControleAutomaticoRPS { get; set; }

    /// <summary>Próximo RPS de produção. Nulo na atualização mantém o valor atual.</summary>
    public int? SequenciaRPSProducao { get; set; }

    /// <summary>Próximo RPS de homologação. Nulo na atualização mantém o valor atual.</summary>
    public int? SequenciaRPSHomologacao { get; set; }

    /// <summary>false mantém a sequência gravada mesmo com valor informado (ela pode ter avançado com emissões); nulo ou true grava.</summary>
    public bool? SequenciaRPSProducaoAlterada { get; set; }

    /// <summary>false mantém a sequência gravada mesmo com valor informado; nulo ou true grava.</summary>
    public bool? SequenciaRPSHomologacaoAlterada { get; set; }

    public bool Ativo { get; set; } = true;

    // Padrões fiscais: a API os aplica às notas importadas por planilha (em NFSe/Emitir, cada nota
    // traz os próprios valores). Ver a observação de Atualizar no <remarks> da classe.

    public int NaturezaOperacaoPadrao { get; set; }
    public bool CalculaRetencoesFederaisPadrao { get; set; }
    public string? TipoRpsPadrao { get; set; }
    public string? SerieRpsPadrao { get; set; }

    /// <summary>Mesma tabela de <see cref="Servico.ExigibilidadeISS"/>.</summary>
    public int ExigibilidadeIssPadrao { get; set; }

    public int TipoTributacaoPadrao { get; set; }

    /// <summary>Mesma tabela de <see cref="Servico.RetemISS"/>.</summary>
    public int IssRetidoPadrao { get; set; }

    public decimal AliquotaIssPadrao { get; set; }
    public string? CodigoServicoPadrao { get; set; }

    public EnderecoEmitente? Endereco { get; set; }
}

public class EnderecoEmitente
{
    public int? Codigo { get; set; }
    public int CodigoEmitente { get; set; }
    public string? Logradouro { get; set; }
    public string? Bairro { get; set; }
    public string? Numero { get; set; }
    public string? Cep { get; set; }
    public string? Complemento { get; set; }

    /// <summary>Define a prefeitura (e a integradora) usada na emissão.</summary>
    public int CodigoIBGEMunicipio { get; set; }

    public int CodigoIBGEUF { get; set; }
}

/// <summary>Resposta de <c>POST /Emitente/Cadastrar</c> e <c>PUT /Emitente/Atualizar</c>.</summary>
public class ResultadoGravacaoEmitente
{
    public string Mensagem { get; set; } = default!;
    public string? CpfCnpj { get; set; }

    /// <summary>Código do emitente, usado nas demais chamadas (ex.: <c>WebhooksClient.ListarPorEmitenteAsync</c>).</summary>
    public int? Codigo { get; set; }
}

/// <summary>
/// Corpo de <c>POST /Emitente/UploadCertificado</c>. Prefira o overload de
/// <c>EmitentesClient.UploadCertificadoAsync</c> que aceita bytes/stream/caminho de arquivo — ele
/// monta este DTO convertendo para Base64 internamente.
/// </summary>
public class DtoUploadCertificado
{
    public string CnpjEmissor { get; set; } = default!;
    public string Base64 { get; set; } = default!;
    public string Senha { get; set; } = default!;
}

/// <summary>Resposta de <c>POST /Emitente/UploadCertificado</c>.</summary>
public class ResultadoUploadCertificado
{
    public string Mensagem { get; set; } = default!;
    public string? ChaveCertificado { get; set; }
    public DateTime? Vencimento { get; set; }

    /// <summary>Alerta não bloqueante sobre o certificado (ex.: vencimento próximo), quando houver.</summary>
    public string? Aviso { get; set; }
}

/// <summary>Item de <c>GET /CoberturaNacional/MunicipiosHomologados</c>.</summary>
public class MunicipioHomologado
{
    /// <summary>Código IBGE (7 dígitos) do município.</summary>
    public int Id { get; set; }

    /// <summary>Rótulo pronto para exibição: <c>"{códigoIBGE} - {nome}"</c>.</summary>
    public string Text { get; set; } = default!;
}
