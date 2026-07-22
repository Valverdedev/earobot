using System.Text.Json;
using Financial.Robot.Application.Backtesting.Interfaces;
using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Application.Backtesting.Services;

public class JsonBacktestDataProvider : IBacktestDataProvider
{
    public async Task<IReadOnlyList<CandleMt5>> LoadDataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Arquivo de backtest não encontrado: {filePath}");

        using var stream = File.OpenRead(filePath);
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var candles = await JsonSerializer.DeserializeAsync<List<CandleMt5>>(stream, options, cancellationToken);
        
        if (candles == null || candles.Count == 0)
            throw new InvalidOperationException("O arquivo JSON de backtest está vazio ou com formato inválido.");

        // Opcional: ordenar cronologicamente caso o JSON não garanta isso
        return candles.OrderBy(c => c.Tempo).ToList();
    }
}
