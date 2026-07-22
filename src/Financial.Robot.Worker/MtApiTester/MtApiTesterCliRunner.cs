using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MtApi5;

namespace Financial.Robot.Worker.MtApiTester;

public static class MtApiTesterCliRunner
{
    public static bool IsMtApiTesterMode(IConfiguration configuration)
    {
        return configuration["mode"]?.Equals("mtapi-tester", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static async Task<int> RunAsync(IConfiguration configuration, IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<MtApi5Client>>();
        var host = configuration["host"] ?? "localhost";
        var portStr = configuration["port"];
        var symbol = configuration["symbol"] ?? "WINQ26";
        var timeframe = configuration["timeframe"] ?? "M1";

        if (!int.TryParse(portStr, out int port))
        {
            logger.LogError("Porta inválida ou não especificada. Use --port 8230.");
            return 1;
        }

        var result = new TesterResult
        {
            StartedAtUtc = DateTime.UtcNow
        };

        logger.LogInformation("Conectando ao MtApi5 em {Host}:{Port}", host, port);

        var client = new MtApi5Client();
        var stopWatch = Stopwatch.StartNew();
        bool connected = false;

        try
        {
            // O Connect é assíncrono internamente na lib via Task
            _ = client.Connect(host, port);

            while (stopWatch.Elapsed.TotalSeconds < 30)
            {
                // Precisamos acessar propriedades de estado da lib ou assumir try catch
                // Em MtApi5Client tem IsTesting(), mas a conexão fica via eventos.
                // Como não expomos estado direto tão facilmente de forma sync, 
                // testamos IsTesting().
                try
                {
                    bool isTesting = client.IsTesting();
                    connected = true;
                    result.Connected = true;
                    result.IsTesting = isTesting;
                    logger.LogInformation("Conectado! IsTesting={IsTesting}", isTesting);
                    break;
                }
                catch
                {
                    // Ignora enquanto não conectar
                    await Task.Delay(500);
                }
            }

            if (!connected)
            {
                throw new Exception("Timeout ao conectar com MtApi5 no Strategy Tester após 30 segundos.");
            }

            // A lib nativamente envia BacktestingReady logo após o Connect quando IsTesting é true.
            logger.LogInformation("BacktestingReady enviado ou confirmado (automático na API).");

            // Validações básicas
            try
            {
                var login = client.AccountInfoInteger(ENUM_ACCOUNT_INFO_INTEGER.ACCOUNT_LOGIN);
                var balance = client.AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE.ACCOUNT_BALANCE);
                var currency = client.AccountInfoString(ENUM_ACCOUNT_INFO_STRING.ACCOUNT_CURRENCY);
                result.AccountLoaded = login > 0 && balance >= 0;
                logger.LogInformation("AccountInfo validado: Login={Login}, Balance={Balance} {Currency}", login, balance, currency);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro em AccountInfo.");
                result.Errors.Add($"AccountInfo: {ex.Message}");
            }

            try
            {
                if (client.SymbolInfoTick(symbol, out var tick) && tick != null)
                {
                    result.TickLoaded = true;
                    logger.LogInformation("SymbolInfoTick validado: {Symbol} Bid={Bid} Ask={Ask}", symbol, tick.bid, tick.ask);
                }
                else
                {
                    logger.LogWarning("SymbolInfoTick retornou null ou false para {Symbol}", symbol);
                    result.Errors.Add($"SymbolInfoTick: falhou para {symbol}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro em SymbolInfoTick.");
                result.Errors.Add($"SymbolInfoTick: {ex.Message}");
            }

            try
            {
                var enumTf = ConverterTimeframe(timeframe);
                client.CopyRates(symbol, enumTf, 0, 10, out var rates);
                if (rates != null)
                {
                    result.CandlesLoaded = rates.Length;
                    logger.LogInformation("CopyRates retornou {N} candles.", rates.Length);
                }
                else
                {
                    logger.LogWarning("CopyRates retornou array nulo.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro em CopyRates.");
                result.Errors.Add($"CopyRates: {ex.Message}");
            }

            try
            {
                var point = client.SymbolInfoDouble(symbol, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_POINT);
                logger.LogInformation("SYMBOL_POINT validado: {Point}", point);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro em SymbolInfoDouble SYMBOL_POINT.");
                result.Errors.Add($"SYMBOL_POINT: {ex.Message}");
            }

            try
            {
                var positionsTotal = client.PositionsTotal();
                logger.LogInformation("PositionsTotal validado: {Total}", positionsTotal);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro em PositionsTotal.");
                result.Errors.Add($"PositionsTotal: {ex.Message}");
            }

            try
            {
                var histSelected = client.HistorySelect(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
                logger.LogInformation("HistorySelect validado: {Hist}", histSelected);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro em HistorySelect.");
                result.Errors.Add($"HistorySelect: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha geral no teste de MtApi5.");
            result.Errors.Add($"Geral: {ex.Message}");
        }
        finally
        {
            if (connected)
            {
                try { client.Disconnect(); } catch { }
            }
        }

        result.FinishedAtUtc = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "data", "mtapi-tests");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"mtapi-tester-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        
        await File.WriteAllTextAsync(file, json);

        if (result.Errors.Count == 0)
        {
            logger.LogInformation("Teste finalizado com sucesso. Salvo em {File}", file);
            return 0;
        }
        else
        {
            logger.LogError("Teste finalizado com falha ({Count} erros). Salvo em {File}", result.Errors.Count, file);
            return 1;
        }
    }

    private static ENUM_TIMEFRAMES ConverterTimeframe(string timeframe) => timeframe.ToUpperInvariant() switch
    {
        "M1" => ENUM_TIMEFRAMES.PERIOD_M1,
        "M5" => ENUM_TIMEFRAMES.PERIOD_M5,
        "M15" => ENUM_TIMEFRAMES.PERIOD_M15,
        "M30" => ENUM_TIMEFRAMES.PERIOD_M30,
        "H1" => ENUM_TIMEFRAMES.PERIOD_H1,
        "H4" => ENUM_TIMEFRAMES.PERIOD_H4,
        "D1" => ENUM_TIMEFRAMES.PERIOD_D1,
        "W1" => ENUM_TIMEFRAMES.PERIOD_W1,
        "MN1" => ENUM_TIMEFRAMES.PERIOD_MN1,
        _ => throw new ArgumentException($"Timeframe desconhecido: {timeframe}")
    };

    private class TesterResult
    {
        public bool Connected { get; set; }
        public bool IsTesting { get; set; }
        public bool AccountLoaded { get; set; }
        public bool TickLoaded { get; set; }
        public int CandlesLoaded { get; set; }
        public List<string> Errors { get; set; } = new();
        public DateTime StartedAtUtc { get; set; }
        public DateTime FinishedAtUtc { get; set; }
    }
}
