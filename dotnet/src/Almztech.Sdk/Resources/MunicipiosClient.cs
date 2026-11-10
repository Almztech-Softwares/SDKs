using Almztech.Sdk.Http;
using Almztech.Sdk.Models;

namespace Almztech.Sdk.Resources;

/// <summary>Consulta de municípios já homologados (rota <c>CoberturaNacional/MunicipiosHomologados</c>).</summary>
public class MunicipiosClient
{
    private readonly AlmztechHttpTransport _transport;

    internal MunicipiosClient(AlmztechHttpTransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Útil para o parceiro validar, antes de emitir, se o município do emitente já é suportado.
    /// Informe o código IBGE ou um termo (parte do nome ou do código); sem nenhum dos dois a lista vem
    /// vazia. Até 20 municípios. Leitura — retry automático seguro.
    /// </summary>
    public Task<List<MunicipioHomologado>> HomologadosAsync(
        int? codigoIbge = null, string? termoDeBusca = null, CancellationToken cancellationToken = default)
    {
        var parametros = new List<string>();
        if (codigoIbge.HasValue)
            parametros.Add($"codigo={codigoIbge.Value}");
        if (!string.IsNullOrWhiteSpace(termoDeBusca))
            parametros.Add($"termoDeBusca={Uri.EscapeDataString(termoDeBusca)}");

        var caminho = "CoberturaNacional/MunicipiosHomologados";
        if (parametros.Count > 0)
            caminho += $"?{string.Join("&", parametros)}";

        return _transport.EnviarAsync<List<MunicipioHomologado>>(HttpMethod.Get, caminho, null, permiteRetry: true, cancellationToken);
    }
}
