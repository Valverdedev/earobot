using Financial.Robot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Risk;

/// <summary>
/// Calcula o volume da ordem respeitando os limites do símbolo na corretora.
/// </summary>
public sealed class CalculadoraLote
{
    private readonly ILogger<CalculadoraLote> _logger;

    public CalculadoraLote(ILogger<CalculadoraLote> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calcula o volume em lotes, respeitando step, min e max da corretora.
    /// </summary>
    public async Task<double> CalcularAsync(
        string modoLote,
        double? loteFixo,
        double? percentualConta,
        double equity,
        double precoAtual,
        double distanciaStop,
        IGatewayMt5 gateway,
        string simbolo,
        CancellationToken ct = default)
    {
        double volume;

        if (modoLote.Equals("percentualConta", StringComparison.OrdinalIgnoreCase) && percentualConta.HasValue && precoAtual > 0)
        {
            throw new InvalidOperationException(
                "Modo percentualConta bloqueado: cálculo seguro exige valor do tick, tamanho do contrato e distância real do stop.");
        }
        else
        {
            volume = loteFixo ?? 0.01;
        }

        // Respeita step, min e max do símbolo da corretora
        var (minLot, maxLot, step) = await gateway.ObterRegrasVolumeAsync(simbolo, ct);
        if (step <= 0) step = 0.01;
        if (minLot <= 0) minLot = 0.01;
        if (maxLot <= 0) maxLot = 100.0;

        volume = Math.Round(volume / step) * step;
        volume = Math.Clamp(volume, minLot, maxLot);

        var decimais = Math.Max(0, (int)Math.Ceiling(-Math.Log10(step)));
        volume = Math.Round(volume, decimais);

        _logger.LogDebug("Lote calculado para {Simbolo}: {Volume} (modo={Modo})", simbolo, volume, modoLote);
        return volume;
    }
}
