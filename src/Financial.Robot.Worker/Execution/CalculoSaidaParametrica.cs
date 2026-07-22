using System;

namespace Financial.Robot.Worker.Execution;

/// <summary>
/// Funções puras de cálculo para saídas paramétricas (percentual, valor bruto), 
/// para permitir teste unitário direto sem dependências de infraestrutura.
/// </summary>
public static class CalculoSaidaParametrica
{
    /// <summary>
    /// Calcula o preço absoluto do limite baseado num percentual sobre o preço de entrada.
    /// Para SL: Compra = preço - (preço * %), Venda = preço + (preço * %)
    /// Para TP: Compra = preço + (preço * %), Venda = preço - (preço * %)
    /// </summary>
    public static double CalcularPrecoPercentual(bool compra, double precoEntrada, decimal percentual, bool isStopLoss)
    {
        double variacao = precoEntrada * (double)percentual / 100.0;
        if (compra)
        {
            return isStopLoss ? precoEntrada - variacao : precoEntrada + variacao;
        }
        else
        {
            return isStopLoss ? precoEntrada + variacao : precoEntrada - variacao;
        }
    }

    /// <summary>
    /// Avalia se o lucro bruto atual (em R$) atingiu o alvo (Take Profit) ou a perda máxima (Stop Loss) em valor bruto.
    /// Retorna verdadeiro se a posição deve ser fechada, considerando que prejuízo (Stop Loss) tem prioridade sobre alvo.
    /// O out parameter 'motivo' retorna uma string descritiva.
    /// </summary>
    public static bool AtingiuLimiteValorBruto(double profitAtual, decimal? tpBruto, decimal? slBruto, out string motivo)
    {
        motivo = string.Empty;

        // slBruto normalmente é configurado positivo ou negativo, assumimos > 0.
        // profitAtual <= -abs(slBruto)
        if (slBruto.HasValue && slBruto.Value > 0)
        {
            double limitePerda = -(double)slBruto.Value;
            if (profitAtual <= limitePerda)
            {
                motivo = $"Saída por valor bruto: profit {profitAtual:F2} <= limite {limitePerda:F2}";
                return true;
            }
        }

        if (tpBruto.HasValue && tpBruto.Value > 0)
        {
            double limiteGanho = (double)tpBruto.Value;
            if (profitAtual >= limiteGanho)
            {
                motivo = $"Saída por valor bruto: profit {profitAtual:F2} >= limite {limiteGanho:F2}";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Avalia se o lucro bruto atual (em R$) atingiu o limite percentual da conta.
    /// </summary>
    public static bool AtingiuLimitePercentualConta(double profitAtual, double saldoConta, decimal? tpPctConta, decimal? slPctConta, out string motivo)
    {
        motivo = string.Empty;
        if (saldoConta <= 0) return false;

        if (slPctConta.HasValue && slPctConta.Value > 0)
        {
            double maxPrejuizo = saldoConta * (double)slPctConta.Value / 100.0;
            if (profitAtual <= -maxPrejuizo)
            {
                motivo = $"Saída por percentual da conta (Stop): profit {profitAtual:F2} <= limite {-maxPrejuizo:F2}";
                return true;
            }
        }

        if (tpPctConta.HasValue && tpPctConta.Value > 0)
        {
            double minLucro = saldoConta * (double)tpPctConta.Value / 100.0;
            if (profitAtual >= minLucro)
            {
                motivo = $"Saída por percentual da conta (Alvo): profit {profitAtual:F2} >= limite {minLucro:F2}";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Calcula o valor absoluto do Trailing Stop baseado em um percentual sobre o preço atual.
    /// Retorna a "distância" em preço, similar ao trailing stop em pips.
    /// </summary>
    public static double CalcularDistanciaTrailingPercentual(double precoAtual, decimal trailingPercentual)
    {
        return precoAtual * (double)trailingPercentual / 100.0;
    }
}
