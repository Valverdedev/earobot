using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Risk;

/// <summary>
/// Avalia todas as travas de risco antes de uma entrada.
/// Cada checagem é isolada e testável individualmente.
/// </summary>
public sealed class RiskGuard
{
    private readonly ILogger<RiskGuard> _logger;

    public RiskGuard(ILogger<RiskGuard> logger)
    {
        _logger = logger;
    }

    /// <summary>Verifica se o horário atual está dentro da janela permitida (UTC).</summary>
    public bool ValidarJanelaHorario(JanelaHorarioConfig? janela, DateTime agoraUtc)
    {
        if (janela is null) return true;

        if (!TimeOnly.TryParse(janela.InicioUtc, out var inicio) ||
            !TimeOnly.TryParse(janela.FimUtc, out var fim))
        {
            _logger.LogWarning("Janela horária inválida (formato HH:mm esperado): {Inicio}-{Fim}", janela.InicioUtc, janela.FimUtc);
            return false;
        }

        var agora = TimeOnly.FromDateTime(agoraUtc);
        var dentro = inicio <= fim ? agora >= inicio && agora <= fim : agora >= inicio || agora <= fim;

        if (!dentro)
            _logger.LogInformation("Entrada bloqueada: fora da janela horária permitida ({Inicio}-{Fim} UTC).", janela.InicioUtc, janela.FimUtc);

        return dentro;
    }

    /// <summary>Verifica se o número de posições abertas não excede o máximo configurado.</summary>
    public bool ValidarMaxOperacoes(int maxPermitido, int abertas, string simbolo, string contexto)
    {
        if (abertas >= maxPermitido)
        {
            _logger.LogInformation("Entrada bloqueada em {Simbolo} [{Contexto}]: {Abertas} posições abertas >= máximo {Max}.", simbolo, contexto, abertas, maxPermitido);
            return false;
        }
        return true;
    }

    /// <summary>Verifica se o drawdown diário ainda está dentro do limite.</summary>
    public bool ValidarDrawdownDiario(double limitePct, double saldoInicialDia, double equityAtual, string simbolo, string contexto)
    {
        if (saldoInicialDia <= 0) return true;

        var drawdownPct = (saldoInicialDia - equityAtual) / saldoInicialDia * 100.0;
        if (drawdownPct >= limitePct)
        {
            _logger.LogWarning("Entrada bloqueada em {Simbolo} [{Contexto}]: drawdown diário {Drawdown:F2}% >= limite {Limite:F2}%.", simbolo, contexto, drawdownPct, limitePct);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Valida SL baseado em ATR contra o spread em tempo real.
    /// Retorna verdadeiro se a entrada é segura ou se não há dados suficientes para decidir.
    /// </summary>
    public bool ValidarSlVsSpread(
        decimal? stopLossAtrMultiplo,
        decimal? slMinimoSobreSpread,
        double atrAtual,
        double spreadAtual,
        string simbolo)
    {
        if (!stopLossAtrMultiplo.HasValue || !slMinimoSobreSpread.HasValue) return true;
        if (spreadAtual <= 0) return true;

        var slImplicito    = (double)stopLossAtrMultiplo.Value * atrAtual;
        var slMinimoExigido = (double)slMinimoSobreSpread.Value * spreadAtual;

        if (slImplicito < slMinimoExigido)
        {
            _logger.LogWarning(
                "Entrada recusada em {Simbolo}: SL implícito ({SlImplicito:F5}) < mínimo exigido sobre spread ({SlMinimo:F5}) [ATR={ATR:F5}, Spread={Spread:F5}, Fator={Fator}x].",
                simbolo, slImplicito, slMinimoExigido, atrAtual, spreadAtual, slMinimoSobreSpread.Value);
            return false;
        }
        return true;
    }

    /// <summary>Verifica se o drawdown de um magic number isolado estourou o limite.</summary>
    public bool ValidarDrawdownIsolado(double limitePct, double saldoBase, double pnlIsolado, string simbolo, string contexto)
    {
        if (saldoBase <= 0) return true;
        
        var drawdownPct = -pnlIsolado / saldoBase * 100.0;
        if (drawdownPct >= limitePct)
        {
            _logger.LogWarning("Entrada bloqueada em {Simbolo} [{Contexto}]: drawdown isolado {Drawdown:F2}% >= limite {Limite:F2}%.", simbolo, contexto, drawdownPct, limitePct);
            return false;
        }
        return true;
    }
}
