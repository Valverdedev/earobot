using Financial.Robot.Application.Constants;
using Microsoft.Extensions.Configuration;

namespace Financial.Robot.Worker.Extract;

public sealed record ExtractOptions(
    string TerminalId,
    string? Symbol,
    string Kind,
    string Timeframe,
    IReadOnlyList<string> Timeframes,
    int Count,
    int TimeoutSeconds,
    string? OutputPath)
{
    public static bool IsExtractMode(IConfiguration configuration)
    {
        return string.Equals(
            configuration["mode"] ?? configuration["Mode"],
            "extract",
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExtractMode(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1].Equals("extract", StringComparison.OrdinalIgnoreCase);

            if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
                return arg["--mode=".Length..].Equals("extract", StringComparison.OrdinalIgnoreCase);

            if (arg.StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
                return arg["mode=".Length..].Equals("extract", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public static ExtractOptions FromConfiguration(IConfiguration configuration)
    {
        var terminalId = configuration["terminal"]
            ?? configuration["terminalId"]
            ?? TerminalDefaults.PrincipalId;

        var kind = configuration["kind"] ?? "snapshot";
        var timeframe = configuration["timeframe"] ?? "M1";
        var count = ReadPositiveInt(configuration, "count", KindRequiresAnalysis(kind) ? 300 : 100);
        var timeoutSeconds = ReadPositiveInt(configuration, "timeout", 15);
        var timeframes = ReadTimeframes(configuration, timeframe);

        return new ExtractOptions(
            terminalId,
            configuration["symbol"],
            kind,
            timeframe,
            timeframes,
            count,
            timeoutSeconds,
            configuration["output"]);
    }

    public bool RequiresSymbol()
    {
        return Kind.Equals("snapshot", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("all", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("tick", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("candles", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("deals", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("positions", StringComparison.OrdinalIgnoreCase)
            || KindRequiresAnalysis(Kind);
    }

    private static int ReadPositiveInt(IConfiguration configuration, string key, int defaultValue)
    {
        return int.TryParse(configuration[key], out var value) && value > 0
            ? value
            : defaultValue;
    }

    private static IReadOnlyList<string> ReadTimeframes(IConfiguration configuration, string fallbackTimeframe)
    {
        var configured = configuration["timeframes"];
        if (string.IsNullOrWhiteSpace(configured))
            configured = configuration["timeframeList"];

        if (string.IsNullOrWhiteSpace(configured))
            return ["M1", "M5", "M15", "H1"];

        var timeframes = configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return timeframes.Count > 0 ? timeframes : [fallbackTimeframe.ToUpperInvariant()];
    }

    private static bool KindRequiresAnalysis(string kind)
    {
        return kind.Equals("analysis", StringComparison.OrdinalIgnoreCase);
    }
}
