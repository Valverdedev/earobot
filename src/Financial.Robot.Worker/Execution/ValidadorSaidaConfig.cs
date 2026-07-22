using System.Collections.Generic;
using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Worker.Execution;

public static class ValidadorSaidaConfig
{
    public static IReadOnlyList<string> Validar(SaidaConfig config)
    {
        var erros = new List<string>();

        if (config == null) return erros;

        // Regra: Valores <= 0 rejeitados
        if (config.StopLossPips <= 0) erros.Add("StopLossPips deve ser maior que zero.");
        if (config.TakeProfitPips <= 0) erros.Add("TakeProfitPips deve ser maior que zero.");
        if (config.StopLossAtrMultiplo <= 0) erros.Add("StopLossAtrMultiplo deve ser maior que zero.");
        if (config.TakeProfitAtrMultiplo <= 0) erros.Add("TakeProfitAtrMultiplo deve ser maior que zero.");
        if (config.StopLossPercentualPreco <= 0) erros.Add("StopLossPercentualPreco deve ser maior que zero.");
        if (config.TakeProfitPercentualPreco <= 0) erros.Add("TakeProfitPercentualPreco deve ser maior que zero.");
        
        if (config.StopLossPercentualConta <= 0) erros.Add("StopLossPercentualConta deve ser maior que zero.");
        if (config.TakeProfitPercentualConta <= 0) erros.Add("TakeProfitPercentualConta deve ser maior que zero.");
        if (config.StopLossValorBruto <= 0) erros.Add("StopLossValorBruto deve ser maior que zero.");
        if (config.TakeProfitValorBruto <= 0) erros.Add("TakeProfitValorBruto deve ser maior que zero.");

        if (config.TrailingStopPips <= 0) erros.Add("TrailingStopPips deve ser maior que zero.");
        if (config.TrailingStopAtrMultiplo <= 0) erros.Add("TrailingStopAtrMultiplo deve ser maior que zero.");
        if (config.TrailingStopPercentualPreco <= 0) erros.Add("TrailingStopPercentualPreco deve ser maior que zero.");

        // Regra: Máx. uma fonte de SL de servidor
        int slSources = 0;
        if (config.StopLossPips > 0) slSources++;
        if (config.StopLossAtrMultiplo > 0) slSources++;
        if (config.StopLossPercentualPreco > 0) slSources++;

        if (slSources > 1)
        {
            erros.Add("Múltiplas fontes de Stop Loss de servidor configuradas (Pips, AtrMultiplo, PercentualPreco). Use no máximo uma.");
        }

        // Regra: Máx. uma fonte de TP de servidor
        int tpSources = 0;
        if (config.TakeProfitPips > 0) tpSources++;
        if (config.TakeProfitAtrMultiplo > 0) tpSources++;
        if (config.TakeProfitPercentualPreco > 0) tpSources++;

        if (tpSources > 1)
        {
            erros.Add("Múltiplas fontes de Take Profit de servidor configuradas (Pips, AtrMultiplo, PercentualPreco). Use no máximo uma.");
        }

        // Regra: Máx. uma fonte de trailing stop
        int trailingSources = 0;
        if (config.TrailingStopPips > 0) trailingSources++;
        if (config.TrailingStopAtrMultiplo > 0) trailingSources++;
        if (config.TrailingStopPercentualPreco > 0) trailingSources++;

        if (trailingSources > 1)
        {
            erros.Add("Múltiplas fontes de Trailing Stop configuradas (Pips, AtrMultiplo, PercentualPreco). Use no máximo uma.");
        }

        return erros;
    }
}
