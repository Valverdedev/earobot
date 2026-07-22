using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Application.Backtesting.Models;

public record BacktestRequest(
    string Symbol,
    EstrategiaConfig EstrategiaConfig,
    string DataFilePath,
    double CapitalInicial = 10000.0,
    double SpreadFixo = 0.0,
    double SlippageFixo = 0.0,
    double ComissaoPorContrato = 0.0
);

public record BacktestTrade(
    DateTime EntryTime,
    DateTime ExitTime,
    LadoOrdem Lado,
    double Volume,
    double EntryPrice,
    double ExitPrice,
    double PnlLiquido,
    string MotivoSaida
);

public record BacktestEquityPoint(
    DateTime Tempo,
    double Equity
);

public record BacktestMetrics(
    int TotalTrades,
    int TradesComLucro,
    int TradesComPrejuizo,
    double TaxaAcerto,
    double LucroLiquidoTotal,
    double ProfitFactor,
    double DrawdownMaximoPercentual,
    double PayoffMedio,
    int MaiorSequenciaPerdas,
    IReadOnlyList<BacktestEquityPoint> CurvaEquity,
    IReadOnlyList<BacktestTrade> Trades
);
