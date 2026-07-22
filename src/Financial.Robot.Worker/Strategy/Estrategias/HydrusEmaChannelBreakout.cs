using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Financial.Robot.Worker.Strategy.Estrategias;

public sealed class HydrusEmaChannelBreakout : IEstrategiaEntrada
{
    public string Nome => "HydrusEmaChannelBreakout";

    public int ObterLookbackNecessario(EstrategiaConfig config)
    {
        var emaPeriodo = ParametroParser.ObterInt(config.Parametros, "emaPeriodo", 156);
        var usarM5 = ParametroParser.ObterString(config.Parametros, "usarConfirmacaoTimeframeMaior", "false").Equals("true", StringComparison.OrdinalIgnoreCase) || 
                     (config.Parametros?.TryGetValue("usarConfirmacaoTimeframeMaior", out var vB) == true && vB is bool b && b);
        var emaConf = ParametroParser.ObterInt(config.Parametros, "emaConfirmacaoPeriodo", 21);
        var tfConfirmacao = ParametroParser.ObterString(config.Parametros, "timeframeConfirmacao", "M5");
        
        int minutos = ObterMinutos(tfConfirmacao);
        
        int lookback = emaPeriodo + 10;
        if (usarM5)
        {
            lookback = Math.Max(lookback, (emaConf + 100) * minutos);
        }
        return lookback;
    }

    private static int ObterMinutos(string timeframe)
    {
        return timeframe.ToUpperInvariant() switch
        {
            "M1" => 1,
            "M5" => 5,
            "M15" => 15,
            "M30" => 30,
            "H1" => 60,
            "H4" => 240,
            "D1" => 1440,
            _ => 5
        };
    }

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var emaPeriodo = ParametroParser.ObterInt(config.Parametros, "emaPeriodo", 156);
        var mediaCurtaPeriodo = ParametroParser.ObterInt(config.Parametros, "mediaCurtaPeriodo", 5);
        var mediaCurtaTipo = ParametroParser.ObterString(config.Parametros, "mediaCurtaTipo", "SMMA").ToUpperInvariant();
        var mediaCurtaPreco = ParametroParser.ObterString(config.Parametros, "mediaCurtaPreco", "Median");
        var canalPontos = ParametroParser.ObterDouble(config.Parametros, "canalPontos", 170.0);
        
        var permitirCompra = config.Comprar;
        var permitirVenda = config.Vender;
        var spreadMaximo = ParametroParser.ObterDouble(config.Parametros, "spreadMaximoPontos", 10.0);
        var distMaxRompimento = ParametroParser.ObterDouble(config.Parametros, "distanciaMaximaAposRompimentoPontos", 80.0);
        
        var usarConfMaior = ParametroParser.ObterString(config.Parametros, "usarConfirmacaoTimeframeMaior", "false").Equals("true", StringComparison.OrdinalIgnoreCase) || 
                            (config.Parametros?.TryGetValue("usarConfirmacaoTimeframeMaior", out var vB) == true && vB is bool b && b);
        var emaConfPeriodo = ParametroParser.ObterInt(config.Parametros, "emaConfirmacaoPeriodo", 21);
        var tfConfirmacao = ParametroParser.ObterString(config.Parametros, "timeframeConfirmacao", "M5");
        var minutosConfirmacao = ObterMinutos(tfConfirmacao);

        if (candles.Count < emaPeriodo + 10)
            return ResultadoDecisao.Aguardar($"Histórico insuficiente (< {emaPeriodo + 10} candles).");

        // 1. Filtro de Spread
        var spreadAtual = tick.Ask - tick.Bid;
        if (spreadAtual > spreadMaximo)
            return ResultadoDecisao.Aguardar($"Spread muito alto ({spreadAtual:F1} > {spreadMaximo}).");

        // 2. Extração de Quotes
        var quotesBase = candles.Select(c => new Quote
        {
            Date = c.Tempo,
            Open = (decimal)c.Abertura,
            High = (decimal)c.Maximo,
            Low = (decimal)c.Minimo,
            Close = (decimal)c.Fechamento, // Default para EMA longa
            Volume = c.Volume
        }).OrderBy(q => q.Date).ToList();

        // 3. Calculo da EMA longa (fechamento)
        var emasLongas = quotesBase.GetEma(emaPeriodo).ToList();
        
        // 4. Calculo da Média Curta com base no Tipo
        var quotesMedian = quotesBase.Select(q => new Quote
        {
            Date = q.Date,
            Open = q.Open,
            High = q.High,
            Low = q.Low,
            Close = mediaCurtaPreco.Equals("Median", StringComparison.OrdinalIgnoreCase) 
                    ? (q.High + q.Low) / 2m 
                    : q.Close,
            Volume = q.Volume
        }).ToList();
        
        double vSmmaAtual = 0;
        double vSmmaAnterior = 0;

        if (mediaCurtaTipo == "SMA")
        {
            var smas = quotesMedian.GetSma(mediaCurtaPeriodo).ToList();
            if (smas[^1].Sma == null || smas[^2].Sma == null) return ResultadoDecisao.Aguardar("Valores nulos SMA");
            vSmmaAtual = (double)smas[^1].Sma.Value;
            vSmmaAnterior = (double)smas[^2].Sma.Value;
        }
        else if (mediaCurtaTipo == "EMA")
        {
            var emas = quotesMedian.GetEma(mediaCurtaPeriodo).ToList();
            if (emas[^1].Ema == null || emas[^2].Ema == null) return ResultadoDecisao.Aguardar("Valores nulos EMA");
            vSmmaAtual = (double)emas[^1].Ema.Value;
            vSmmaAnterior = (double)emas[^2].Ema.Value;
        }
        else
        {
            var smmas = quotesMedian.GetSmma(mediaCurtaPeriodo).ToList();
            if (smmas[^1].Smma == null || smmas[^2].Smma == null) return ResultadoDecisao.Aguardar("Valores nulos SMMA");
            vSmmaAtual = (double)smmas[^1].Smma.Value;
            vSmmaAnterior = (double)smmas[^2].Smma.Value;
        }

        var ultimaEma = emasLongas[^1];
        var emaAnterior = emasLongas[^2];

        if (ultimaEma.Ema == null || emaAnterior.Ema == null)
            return ResultadoDecisao.Aguardar("Falha ao calcular indicadores (Valores nulos na EMA longa).");

        // 5. Confirmação M5 (se ativado)
        if (usarConfMaior)
        {
            // Otimização: processa apenas os últimos candles M1 necessários para estabilizar o EMA no timeframe maior
            int candlesNecessariosM1 = (emaConfPeriodo + 100) * minutosConfirmacao;
            if (candles.Count < candlesNecessariosM1)
                return ResultadoDecisao.Aguardar($"Histórico insuficiente para confirmação {tfConfirmacao} (< {candlesNecessariosM1} M1)");

            var candlesRecentesM1 = candles.TakeLast(candlesNecessariosM1).ToList();
            var quotesTfMaior = AgregadorTimeframe.AgruparM1ParaTimeframeMaior(candlesRecentesM1, tfConfirmacao);

            if (quotesTfMaior.Count > emaConfPeriodo)
            {
                var emaM5List = quotesTfMaior.GetEma(emaConfPeriodo).ToList();
                var ultimaEmaM5 = emaM5List[^1].Ema;
                if (ultimaEmaM5 != null)
                {
                    bool exigirPrecoAcima = ParametroParser.ObterString(config.Parametros, "exigirPrecoAcimaEmaParaCompra", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
                    bool exigirPrecoAbaixo = ParametroParser.ObterString(config.Parametros, "exigirPrecoAbaixoEmaParaVenda", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
                    
                    if (permitirCompra && exigirPrecoAcima && tick.Bid < (double)ultimaEmaM5.Value)
                        permitirCompra = false;
                        
                    if (permitirVenda && exigirPrecoAbaixo && tick.Bid > (double)ultimaEmaM5.Value)
                        permitirVenda = false;
                }
            }
        }

        // 6. Lógica de Canal
        double bandaSupAtual = (double)ultimaEma.Ema.Value + canalPontos;
        double bandaSupAnterior = (double)emaAnterior.Ema.Value + canalPontos;
        
        double bandaInfAtual = (double)ultimaEma.Ema.Value - canalPontos;
        double bandaInfAnterior = (double)emaAnterior.Ema.Value - canalPontos;

        // Regra de Compra: SMMA cruzou Banda Superior para cima
        bool cruzouAlta = vSmmaAnterior <= bandaSupAnterior && vSmmaAtual > bandaSupAtual;
        
        // Regra de Venda: SMMA cruzou Banda Inferior para baixo
        bool cruzouBaixa = vSmmaAnterior >= bandaInfAnterior && vSmmaAtual < bandaInfAtual;

        // Distância após rompimento
        if (permitirCompra && cruzouAlta)
        {
            double distRompimento = tick.Ask - bandaSupAtual;
            if (distRompimento > distMaxRompimento)
                return ResultadoDecisao.Aguardar($"Cruzamento de alta ignorado (Preço muito longe da banda: dist={distRompimento:F1} > {distMaxRompimento}).");
            
            return ResultadoDecisao.Comprar("Cruzamento banda superior (HYDRUS).");
        }

        if (permitirVenda && cruzouBaixa)
        {
            double distRompimento = bandaInfAtual - tick.Bid;
            if (distRompimento > distMaxRompimento)
                return ResultadoDecisao.Aguardar($"Cruzamento de baixa ignorado (Preço muito longe da banda: dist={distRompimento:F1} > {distMaxRompimento}).");
                
            return ResultadoDecisao.Vender("Cruzamento banda inferior (HYDRUS).");
        }

        return ResultadoDecisao.Aguardar($"Aguardando cruzamento das bandas ({mediaCurtaTipo} vs EMA+Canal).");
    }
}
