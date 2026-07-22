using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Application.Backtesting.Interfaces;

public interface IBacktestDataProvider
{
    Task<IReadOnlyList<CandleMt5>> LoadDataAsync(string filePath, CancellationToken cancellationToken = default);
}
