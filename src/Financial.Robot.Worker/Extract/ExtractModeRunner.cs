using System.Text.Json;
using System.Text.Json.Serialization;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Infrastructure.Gateways;
using Financial.Robot.Infrastructure.Mt5;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Financial.Robot.Worker.Extract;

public static class ExtractModeRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> RunAsync(IConfiguration configuration, CancellationToken ct = default)
    {
        var options = ExtractOptions.FromConfiguration(configuration);

        if (options.RequiresSymbol() && string.IsNullOrWhiteSpace(options.Symbol))
        {
            await Console.Error.WriteLineAsync("Modo extract requer --symbol para kind snapshot/all/tick/candles/positions/analysis.");
            return 2;
        }

        var terminal = ResolveTerminal(configuration, options.TerminalId);
        if (terminal is null)
        {
            await Console.Error.WriteLineAsync($"Terminal '{options.TerminalId}' não encontrado na seção Terminals.");
            return 2;
        }

        await using var gateway = new GatewayMt5(NullLogger<GatewayMt5>.Instance);

        try
        {
            await gateway.ConectarAsync(terminal.Host, terminal.Port, options.TimeoutSeconds, ct);

            var result = await ExtractAsync(gateway, terminal, options, ct);
            var json = JsonSerializer.Serialize(result, JsonOptions);

            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                await Console.Out.WriteLineAsync(json);
            }
            else
            {
                var outputPath = Path.GetFullPath(options.OutputPath);
                var outputDirectory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                await File.WriteAllTextAsync(outputPath, json, ct);
                await Console.Error.WriteLineAsync($"Extração MT5 salva em: {outputPath}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Falha no modo extract: {ex.Message}");
            return 1;
        }
        finally
        {
            try
            {
                await gateway.DesconectarAsync(ct);
            }
            catch
            {
                // O processo está encerrando; falha de desconexão não deve ocultar o resultado da extração.
            }
        }
    }

    private static async Task<ExtractResult> ExtractAsync(
        IGatewayMt5 gateway,
        TerminalConfig terminal,
        ExtractOptions options,
        CancellationToken ct)
    {
        var kind = options.Kind.ToLowerInvariant();

        InfoContaMt5? account = null;
        TickMt5? tick = null;
        SymbolMetadataDto? metadata = null;
        IReadOnlyList<CandleMt5>? candles = null;
        IReadOnlyDictionary<string, IReadOnlyList<CandleMt5>>? timeframes = null;
        IReadOnlyList<DetalhesPosicaoMt5>? positions = null;
        IReadOnlyList<DealMt5>? deals = null;
        AnalysisContextDto? analysisContext = null;

        if (kind is "snapshot" or "all" or "account" or "analysis")
            account = await gateway.ObterInfoContaAsync(ct);

        if (!string.IsNullOrWhiteSpace(options.Symbol))
        {
            if (kind is "snapshot" or "all" or "tick" or "analysis")
                tick = await gateway.ObterTickAtualAsync(options.Symbol, ct);

            if (kind is "snapshot" or "all" or "analysis")
                metadata = await ReadSymbolMetadataAsync(gateway, options.Symbol, ct);

            if (kind is "snapshot" or "all" or "candles")
                candles = await gateway.ObterCandlesAsync(options.Symbol, options.Timeframe, options.Count, ct);

            if (kind is "analysis")
            {
                timeframes = await ReadTimeframesAsync(gateway, options, ct);
            }

            if (kind is "snapshot" or "all" or "positions" or "analysis")
                positions = await gateway.ObterDetalhesPosicoesAbertasAsync(options.Symbol, magicNumber: null, ct);

            if (kind is "deals" or "all")
            {
                var inicio = DateTime.UtcNow.Date.AddDays(-7);
                var fim = DateTime.UtcNow.AddDays(1);
                deals = await gateway.ObterDealsHistoricosAsync(options.Symbol, magicNumber: null, inicio, fim, ct);
            }

            if (kind is "analysis" && timeframes is not null)
            {
                analysisContext = AnalysisContextBuilder.Build(
                    tick,
                    metadata,
                    timeframes,
                    positions,
                    DateTime.UtcNow);
            }
        }

        return new ExtractResult(
            DateTime.UtcNow,
            terminal.TerminalId,
            terminal.Host,
            terminal.Port,
            options.Kind,
            options.Symbol,
            options.Timeframe,
            options.Count,
            account,
            tick,
            metadata,
            candles,
            timeframes,
            positions,
            deals,
            analysisContext);
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<CandleMt5>>> ReadTimeframesAsync(
        IGatewayMt5 gateway,
        ExtractOptions options,
        CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlyList<CandleMt5>>(StringComparer.OrdinalIgnoreCase);

        foreach (var timeframe in options.Timeframes)
        {
            result[timeframe] = await gateway.ObterCandlesAsync(
                options.Symbol!,
                timeframe,
                options.Count,
                ct);
        }

        return result;
    }

    private static async Task<SymbolMetadataDto> ReadSymbolMetadataAsync(
        IGatewayMt5 gateway,
        string symbol,
        CancellationToken ct)
    {
        var pointSize = await gateway.ObterTamanhoPontoAsync(symbol, ct);
        var stopsLevel = await gateway.ObterStopsLevelAsync(symbol, ct);
        var (minVolume, maxVolume, volumeStep) = await gateway.ObterRegrasVolumeAsync(symbol, ct);

        return new SymbolMetadataDto(
            pointSize,
            stopsLevel,
            minVolume,
            maxVolume,
            volumeStep);
    }

    private static TerminalConfig? ResolveTerminal(IConfiguration configuration, string terminalId)
    {
        var terminals = configuration.GetSection("Terminals").Get<List<TerminalConfig>>() ?? [];

        return terminals.FirstOrDefault(t =>
            t.TerminalId.Equals(terminalId, StringComparison.OrdinalIgnoreCase));
    }
}
