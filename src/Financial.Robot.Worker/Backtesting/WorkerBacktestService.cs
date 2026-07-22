using Financial.Robot.Application.Backtesting.Interfaces;
using Financial.Robot.Application.Backtesting.Models;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using Financial.Robot.Worker.Strategy;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Backtesting;

public class WorkerBacktestService : IBacktestService
{
    private readonly IBacktestDataProvider _dataProvider;
    private readonly IBacktestExecutionSimulator _simulator;
    private readonly CatalogoEstrategias _catalogoEstrategias;
    private readonly CatalogoIndicadores _catalogoIndicadores;
    private readonly ILogger<WorkerBacktestService> _logger;

    public WorkerBacktestService(
        IBacktestDataProvider dataProvider,
        IBacktestExecutionSimulator simulator,
        CatalogoEstrategias catalogoEstrategias,
        CatalogoIndicadores catalogoIndicadores,
        ILogger<WorkerBacktestService> logger)
    {
        _dataProvider = dataProvider;
        _simulator = simulator;
        _catalogoEstrategias = catalogoEstrategias;
        _catalogoIndicadores = catalogoIndicadores;
        _logger = logger;
    }

    public async Task<BacktestMetrics> RunAsync(BacktestRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando backtest para {Symbol} com estratégia {Estrategia}", request.Symbol, request.EstrategiaConfig.Nome);

        var estrategia = _catalogoEstrategias.Resolver(request.EstrategiaConfig.Nome);
        if (estrategia == null)
            throw new InvalidOperationException($"Estratégia {request.EstrategiaConfig.Nome} não encontrada no catálogo.");

        var candles = await _dataProvider.LoadDataAsync(request.DataFilePath, cancellationToken);
        _logger.LogInformation("Dados carregados: {Count} candles.", candles.Count);

        _simulator.Initialize(request.CapitalInicial, request.SpreadFixo, request.SlippageFixo, request.ComissaoPorContrato);

        var lookbackNecessario = StrategyEngine.CalcularMaxPeriodoRequerido(request.EstrategiaConfig, estrategia);
        
        // Iteramos do lookback até o final
        for (int i = lookbackNecessario; i < candles.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var currentCandle = candles[i];
            
            // 1. O simulador processa a movimentação intra-candle para verificar SL/TP da posição já aberta
            _simulator.ProcessCandle(currentCandle);

            // 2. Extraímos a janela de histórico até o candle ATUAL para a estratégia avaliar (fechamento)
            var slice = ((List<CandleMt5>)candles).GetRange(i - lookbackNecessario, lookbackNecessario + 1);

            // 3. Calculamos indicadores configurados no JSON
            var indicadoresResult = new List<(IndicadorConfig, ResultadoIndicador)>();
            if (request.EstrategiaConfig.Indicadores?.Any() == true)
            {
                // Apenas agrupa o timeframe base, não suporta MTF complexo via JSON no simulador ainda,
                // mas a estratégia usa Skender interno.
                indicadoresResult.AddRange(_catalogoIndicadores.CalcularTodos(request.EstrategiaConfig.Indicadores, slice));
            }

            // Criamos um tick falso baseado no fechamento do candle para a avaliação
            var mockTick = new TickEvent(
                TerminalId: "BACKTEST",
                Symbol: request.Symbol,
                Bid: currentCandle.Fechamento - request.SpreadFixo / 2.0,
                Ask: currentCandle.Fechamento + request.SpreadFixo / 2.0,
                Timestamp: currentCandle.Tempo
            );

            // 4. Avalia a estratégia
            var decisao = estrategia.Avaliar(slice, indicadoresResult, new List<(IndicadorConfig, ResultadoIndicador)>(), mockTick, request.EstrategiaConfig);

            // 5. Se houver sinal, tentar abrir ordem (caso não haja posição)
            if (decisao.Executar && decisao.Lado.HasValue)
            {
                // Cálculo de Lote estático simplificado para Backtest MVP
                double lote = 1.0; 
                if (request.EstrategiaConfig.GestaoDeRisco?.LoteFixo.HasValue == true)
                    lote = request.EstrategiaConfig.GestaoDeRisco.LoteFixo.Value;

                // StopLoss/TakeProfit pips (simples)
                double? sl = null;
                double? tp = null;
                if (request.EstrategiaConfig.Saida?.StopLossPips.HasValue == true)
                {
                    double distSl = (double)request.EstrategiaConfig.Saida.StopLossPips.Value;
                    sl = decisao.Lado.Value == LadoOrdem.Compra ? currentCandle.Fechamento - distSl : currentCandle.Fechamento + distSl;
                    
                    if (request.EstrategiaConfig.Saida.TakeProfitPips.HasValue)
                    {
                        double distTp = (double)request.EstrategiaConfig.Saida.TakeProfitPips.Value;
                        tp = decisao.Lado.Value == LadoOrdem.Compra ? currentCandle.Fechamento + distTp : currentCandle.Fechamento - distTp;
                    }
                }
                else if (request.EstrategiaConfig.Saida?.StopLossPercentualPreco.HasValue == true)
                {
                    sl = Execution.CalculoSaidaParametrica.CalcularPrecoPercentual(decisao.Lado.Value == LadoOrdem.Compra, currentCandle.Fechamento, request.EstrategiaConfig.Saida.StopLossPercentualPreco.Value, true);
                    
                    if (request.EstrategiaConfig.Saida.TakeProfitPercentualPreco.HasValue)
                    {
                        tp = Execution.CalculoSaidaParametrica.CalcularPrecoPercentual(decisao.Lado.Value == LadoOrdem.Compra, currentCandle.Fechamento, request.EstrategiaConfig.Saida.TakeProfitPercentualPreco.Value, false);
                    }
                }

                _simulator.ExecuteOrder(decisao.Lado.Value, lote, sl, tp, currentCandle);
            }
        }

        // Forçar fechamento da última posição aberta se houver, usando o último candle
        _simulator.CloseAllPositions(candles[^1], "EndOfData");

        var metrics = _simulator.GetMetrics();
        _logger.LogInformation("Backtest finalizado. Total Trades: {TotalTrades}, PnL Líquido: {PnL}", metrics.TotalTrades, metrics.LucroLiquidoTotal);

        return metrics;
    }
}
