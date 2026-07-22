using Financial.Robot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Indicators;

public sealed class ForcaCestaIndicador : IIndicadorMultiFonte
{
    private readonly ILogger<ForcaCestaIndicador> _logger;

    public ForcaCestaIndicador(ILogger<ForcaCestaIndicador> logger)
    {
        _logger = logger;
    }

    public string Nome => "ForcaCesta";

    public async Task<ResultadoIndicador> CalcularAsync(
        IReadOnlyDictionary<string, IGatewayMt5> gatewaysPorTerminal,
        IDictionary<string, object>? parametros,
        CancellationToken ct)
    {
        var terminalId  = ParametroParser.ObterString(parametros, "terminalId", "genial");
        var timeframe   = ParametroParser.ObterString(parametros, "timeframe", "D1");
        var amplitudeNorm = ParametroParser.ObterDouble(parametros, "amplitudeNormalizacaoPercent", 3.0);
        var componentes = ParametroParser.ObterComponentes(parametros, "componentes");
        var janelaAvista = ParametroParser.ObterJanelaHorario(parametros, "exigeMercadoAvistaAberto");

        if (janelaAvista.HasValue)
        {
            var agora = DateTime.UtcNow.TimeOfDay;
            var inicio = janelaAvista.Value.Inicio;
            var fim = janelaAvista.Value.Fim;
            var dentro = inicio <= fim ? agora >= inicio && agora <= fim : agora >= inicio || agora <= fim;
            if (!dentro)
            {
                _logger.LogInformation("ForcaCesta: Fora da janela de mercado à vista ({Inicio}-{Fim} UTC). Ignorando cálculo.", inicio, fim);
                return new ResultadoIndicador(DateTime.UtcNow, null);
            }
        }

        if (!gatewaysPorTerminal.TryGetValue(terminalId, out var gateway))
        {
            _logger.LogWarning("ForcaCesta: Gateway '{TerminalId}' não encontrado para cálculo.", terminalId);
            return new ResultadoIndicador(DateTime.UtcNow, null);
        }

        double scoreAcumulado = 0;
        double pesoTotal = 0;

        foreach (var (simbolo, peso) in componentes)
        {
            try
            {
                var candles = await gateway.ObterCandlesAsync(simbolo, timeframe, 5, ct);
                var ultimos = candles.OrderBy(c => c.Tempo).ToList();
                if (ultimos.Count < 2) continue;

                var fechamentoAnterior = ultimos[^2].Fechamento;
                var fechamentoAtual    = ultimos[^1].Fechamento;
                if (fechamentoAnterior == 0) continue;

                var variacaoPercent = (fechamentoAtual - fechamentoAnterior) / fechamentoAnterior * 100.0;
                var scoreComponente = Math.Clamp(variacaoPercent / amplitudeNorm * 100.0, -100, 100);

                scoreAcumulado += scoreComponente * peso;
                pesoTotal += peso;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ForcaCesta: Falha ao obter dados da componente {Simbolo}.", simbolo);
            }
        }

        if (pesoTotal == 0) return new ResultadoIndicador(DateTime.UtcNow, null);

        var scoreFinal = scoreAcumulado / pesoTotal;
        return new ResultadoIndicador(DateTime.UtcNow, scoreFinal);
    }
}
