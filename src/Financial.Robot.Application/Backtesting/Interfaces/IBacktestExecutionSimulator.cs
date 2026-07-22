using Financial.Robot.Application.Backtesting.Models;
using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Application.Backtesting.Interfaces;

public interface IBacktestExecutionSimulator
{
    void Initialize(double capitalInicial, double spreadFixo, double slippageFixo, double comissaoPorContrato);
    
    // Processa a movimentação do candle atual, verificando toques em SL/TP de ordens já abertas
    void ProcessCandle(CandleMt5 candle);
    
    // Tenta abrir uma nova ordem, sujeita a fundos, spread e slippage
    void ExecuteOrder(LadoOrdem lado, double volume, double? stopLoss, double? takeProfit, CandleMt5 executionCandle);
    
    // Force fechamento de posições abertas no final dos dados
    void CloseAllPositions(CandleMt5 finalCandle, string motivo);
    
    BacktestMetrics GetMetrics();
}
