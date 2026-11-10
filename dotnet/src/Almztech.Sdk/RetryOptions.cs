namespace Almztech.Sdk;

/// <summary>
/// Política de retry aplicada apenas a operações seguras (GET e <c>ConsultarComFiltros</c> — ver
/// DESIGN.md seção 4). Nunca se aplica a <c>Emitir</c>/<c>Cancelar</c>, que não têm idempotency
/// key nativa na API.
/// </summary>
public class RetryOptions
{
    public static RetryOptions Default => new();

    public int MaxAttempts { get; set; } = 3;
    public TimeSpan IntervaloInicial { get; set; } = TimeSpan.FromMilliseconds(500);
    public double Multiplicador { get; set; } = 2.0;
    public TimeSpan IntervaloMaximo { get; set; } = TimeSpan.FromSeconds(10);

    internal TimeSpan CalcularAtraso(int tentativa)
    {
        var baseDelayMs = IntervaloInicial.TotalMilliseconds * Math.Pow(Multiplicador, tentativa - 1);
        var cappedMs = Math.Min(baseDelayMs, IntervaloMaximo.TotalMilliseconds);
        var jitterMs = Random.Shared.NextDouble() * cappedMs * 0.2;
        return TimeSpan.FromMilliseconds(cappedMs + jitterMs);
    }
}
