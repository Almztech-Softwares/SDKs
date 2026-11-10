using System.Text.Json.Serialization;

namespace Almztech.Sdk.Models;

/// <summary>
/// Status de um <see cref="ProtocoloResponse"/>. Serializado como string (nome do membro) no JSON,
/// idêntico aos valores reais da API (confirmado empiricamente, ver DESIGN.md seção 0).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StatusProtocolo
{
    EM_FILA,
    EM_PROCESSAMENTO,
    PENDENTE_PREFEITURA,
    PROCESSADO_COM_SUCESSO,
    REJEITADO,
    CANCELADO,
    ERRO_INTERNO,
}

/// <summary>Status de uma nota individual dentro de um protocolo. Ver <see cref="ProtocoloNotaResponse.Situacao"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StatusNota
{
    EM_PROCESSAMENTO,
    EMITIDA,
    CANCELADA,
    DENEGADA,
    REJEITADA,
    PENDENTE_PREFEITURA,
}
