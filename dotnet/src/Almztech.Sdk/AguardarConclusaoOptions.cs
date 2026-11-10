using Almztech.Sdk.Models;

namespace Almztech.Sdk;

/// <summary>Opções do helper de polling <c>NotasFiscaisClient.AguardarConclusaoAsync</c>. Ver DESIGN.md seção 4.</summary>
public class AguardarConclusaoOptions
{
    public TimeSpan IntervaloInicial { get; set; } = TimeSpan.FromSeconds(2);
    public double BackoffMultiplicador { get; set; } = 1.5;
    public TimeSpan IntervaloMaximo { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan TimeoutTotal { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Chamado a cada consulta de status, mesmo em estados não-terminais — útil para UI tipo "aguarde, emitindo sua nota".</summary>
    public Action<ProtocoloResponse>? OnTentativa { get; set; }
}
