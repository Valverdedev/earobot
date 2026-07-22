using System.Text.Json;
using Financial.Robot.Application.Backtesting.Interfaces;
using Financial.Robot.Application.Backtesting.Models;
using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Backtesting;

public class BacktestCliRunner
{
    public static async Task<int> RunAsync(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<BacktestCliRunner>>();
        var backtestService = serviceProvider.GetRequiredService<IBacktestService>();

        var configPath = configuration["config"];
        var dataPath = configuration["data"];

        if (string.IsNullOrWhiteSpace(configPath) || string.IsNullOrWhiteSpace(dataPath))
        {
            logger.LogError("Parâmetros --config ou --data ausentes. Exemplo: dotnet run --mode backtest --config config/WINQ26.config.json --data data/win_m1.json");
            return 1;
        }

        logger.LogInformation("Iniciando Modo Backtest...");
        logger.LogInformation("Configuração: {ConfigPath}", configPath);
        logger.LogInformation("Dados: {DataPath}", dataPath);

        // 1. Carregar Config
        var configJson = await File.ReadAllTextAsync(configPath);
        var symbolConfig = JsonSerializer.Deserialize<SymbolConfig>(configJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (symbolConfig == null || symbolConfig.Estrategias == null || !symbolConfig.Estrategias.Any())
        {
            logger.LogError("Configuração inválida ou sem estratégias ativas.");
            return 1;
        }

        // Executar backtest para a primeira estratégia encontrada para simplificar o MVP
        var estrategiaConfig = symbolConfig.Estrategias.First();

        var request = new BacktestRequest(
            Symbol: symbolConfig.BrokerSymbol ?? symbolConfig.Symbol,
            EstrategiaConfig: estrategiaConfig,
            DataFilePath: dataPath,
            CapitalInicial: 10000.0,
            SpreadFixo: 0.0, // Configurável no futuro via CLI arg ou SymbolConfig
            SlippageFixo: 0.0,
            ComissaoPorContrato: 0.0
        );

        try
        {
            var metrics = await backtestService.RunAsync(request);

            logger.LogInformation("================ RELATÓRIO DE BACKTEST ================");
            logger.LogInformation("Símbolo:        {Symbol}", request.Symbol);
            logger.LogInformation("Estratégia:     {Strategy}", request.EstrategiaConfig.Nome);
            logger.LogInformation("Total Trades:   {Total}", metrics.TotalTrades);
            logger.LogInformation("Win Rate:       {WinRate:P2}", metrics.TaxaAcerto);
            logger.LogInformation("Lucro Líquido:  R$ {NetProfit:F2}", metrics.LucroLiquidoTotal);
            logger.LogInformation("Profit Factor:  {ProfitFactor:F2}", metrics.ProfitFactor);
            logger.LogInformation("Drawdown Max:   {Drawdown:F2}%", metrics.DrawdownMaximoPercentual);
            logger.LogInformation("Payoff Médio:   {Payoff:F2}", metrics.PayoffMedio);
            logger.LogInformation("Loss Streak:    {LossStreak}", metrics.MaiorSequenciaPerdas);
            logger.LogInformation("=======================================================");

            // Salvar resultado em JSON
            var resultDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "backtests", "results");
            Directory.CreateDirectory(resultDir);
            
            var resultFileName = $"backtest_{request.Symbol}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var resultPath = Path.Combine(resultDir, resultFileName);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(metrics, jsonOptions));

            logger.LogInformation("Relatório completo salvo em: {ResultPath}", resultPath);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro crítico durante o backtest.");
            return 1;
        }
    }

    public static bool IsBacktestMode(string[] args)
    {
        return args.Contains("--mode") && args.SkipWhile(a => a != "--mode").Skip(1).FirstOrDefault() == "backtest";
    }

    public static bool IsBacktestMode(IConfiguration config)
    {
        return config["mode"]?.Equals("backtest", StringComparison.OrdinalIgnoreCase) == true;
    }
}
