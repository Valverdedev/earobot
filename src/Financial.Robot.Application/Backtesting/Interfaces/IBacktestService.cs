using Financial.Robot.Application.Backtesting.Models;

namespace Financial.Robot.Application.Backtesting.Interfaces;

public interface IBacktestService
{
    Task<BacktestMetrics> RunAsync(BacktestRequest request, CancellationToken cancellationToken = default);
}
