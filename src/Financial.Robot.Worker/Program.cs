using Financial.Robot.Application.Configuracoes;
using Financial.Robot.Application.DependencyInjection;
using Financial.Robot.Infrastructure.DependencyInjection;
using Financial.Robot.Worker.Extract;
using Financial.Robot.Worker.Backtesting;
using Financial.Robot.Application.Backtesting.Interfaces;
using Financial.Robot.Application.Backtesting.Services;
using Financial.Robot.Worker.MtApiTester;
using Serilog;

// ─── Configuração inicial do Serilog ────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var bootstrapExtractMode = ExtractOptions.IsExtractMode(args);
    if (!bootstrapExtractMode)
        Log.Information("financial.robot iniciando...");

    var builder = Host.CreateApplicationBuilder(args);

    // Permitir carregar configurações de um arquivo extra (ex: --config=config/test-mode.config.json)
    var customConfigPath = builder.Configuration["config"];
    if (!string.IsNullOrWhiteSpace(customConfigPath))
    {
        var basePath = Directory.GetCurrentDirectory();
        var fullPath = Path.GetFullPath(Path.Combine(basePath, customConfigPath));

        // Se não encontrar na pasta do projeto (caso do dotnet run), tenta subir 2 níveis (raiz da solution)
        if (!File.Exists(fullPath))
        {
            fullPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", customConfigPath));
        }

        builder.Configuration.AddJsonFile(fullPath, optional: false, reloadOnChange: true);
    }

    if (ExtractOptions.IsExtractMode(builder.Configuration))
    {
        Environment.ExitCode = await ExtractModeRunner.RunAsync(builder.Configuration);
        return;
    }

    if (BacktestCliRunner.IsBacktestMode(builder.Configuration))
    {
        // ─── Configurações e Serilog Minimalista para Backtest ─────────────────
        builder.Services.AddSerilog((servicos, loggerConfig) =>
        {
            loggerConfig.MinimumLevel.Is(Serilog.Events.LogEventLevel.Information)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        });

        builder.Services.AddSingleton<Financial.Robot.Worker.Indicators.CatalogoIndicadores>();
        builder.Services.AddSingleton<Financial.Robot.Worker.Strategy.CatalogoEstrategias>(sp =>
        {
            var catalogo = new Financial.Robot.Worker.Strategy.CatalogoEstrategias();
            catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.CruzamentoEma());
            catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.PriceActionSuporteResistencia());
            catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.OpeningRangeBreakout());
            catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.ReversaoRange());
            catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.ScalperWinPullbackCurto());
            catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.HydrusEmaChannelBreakout());
            catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.MicroTendenciaPullbackEma());
            catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.PriceActionBarByBar());
            catalogo.Registrar(new Financial.Robot.Worker.Strategy.Interpretada.EstrategiaInterpretada());
            return catalogo;
        });

        builder.Services.AddSingleton<IBacktestDataProvider, JsonBacktestDataProvider>();
        builder.Services.AddTransient<IBacktestExecutionSimulator, ExecutionSimulator>();
        builder.Services.AddSingleton<IBacktestService, WorkerBacktestService>();

        var hostBacktest = builder.Build();
        Environment.ExitCode = await BacktestCliRunner.RunAsync(builder.Configuration, hostBacktest.Services);
        return;
    }

    if (MtApiTesterCliRunner.IsMtApiTesterMode(builder.Configuration))
    {
        builder.Services.AddSerilog((servicos, loggerConfig) =>
        {
            loggerConfig.MinimumLevel.Is(Serilog.Events.LogEventLevel.Information)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        });

        // Registrar o MtApi5Client logger que o Runner solicita
        builder.Services.AddLogging();
        var hostTester = builder.Build();
        Environment.ExitCode = await MtApiTesterCliRunner.RunAsync(builder.Configuration, hostTester.Services);
        return;
    }

    // ─── Serilog ────────────────────────────────────────────────────────────
    builder.Services.AddSerilog((servicos, loggerConfig) =>
    {
        var nivelMinimo = builder.Configuration["logging:minimumLevel"] ?? "Information";
        var caminhoArquivo = builder.Configuration["logging:filePath"]
            ?? "logs/financial-robot-.log";

        loggerConfig
            .MinimumLevel.Is(Serilog.Events.LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                caminhoArquivo,
                rollingInterval: RollingInterval.Day,
                outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
    });

    // ─── Configurações tipadas ───────────────────────────────────────────────
    builder.Services.Configure<ConfiguracaoModoTeste>(
        builder.Configuration);

    builder.Services.Configure<List<Financial.Robot.Application.Configuracoes.ConfiguracaoAlvoGlobalPosicoesAbertas>>(
        builder.Configuration.GetSection("AlvoGlobalPosicoesAbertas"));

    // ─── Camadas DDD ────────────────────────────────────────────────────────
    builder.Services
        .AdicionarInfraestrutura()
        .AdicionarApplication();

    // ─── Fase 2: Serviços ───────────────────────────────────────────────────
    builder.Services.AddSingleton<Financial.Robot.Worker.Config.ConfigValidator>();
    // ConfigWatcherService precisa ser Singleton para que a Factory possa se inscrever no evento ConfigChanged
    builder.Services.AddSingleton<Financial.Robot.Worker.Config.ConfigWatcherService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<Financial.Robot.Worker.Config.ConfigWatcherService>());
    builder.Services.AddSingleton<Financial.Robot.Application.Interfaces.IMarketDataService, Financial.Robot.Worker.MarketData.MarketDataService>();
    builder.Services.AddHostedService(sp => (Financial.Robot.Worker.MarketData.MarketDataService)sp.GetRequiredService<Financial.Robot.Application.Interfaces.IMarketDataService>());

    // ─── Fase 3: Strategy Engine ─────────────────────────────────────────────────
    builder.Services.AddSingleton<Financial.Robot.Worker.Indicators.CatalogoIndicadores>();
    builder.Services.AddSingleton<Financial.Robot.Worker.Indicators.CatalogoIndicadoresMultiFonte>();

    // Catalogo de Estratégias
    builder.Services.AddSingleton(sp =>
    {
        var catalogo = new Financial.Robot.Worker.Strategy.CatalogoEstrategias();
        catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.CruzamentoEma());
        catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.PriceActionSuporteResistencia());
        catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.OpeningRangeBreakout());
        catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.ReversaoRange());
        catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.ScalperWinPullbackCurto());
        catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.HydrusEmaChannelBreakout());
        catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.MicroTendenciaPullbackEma());
        catalogo.Registrar(new Financial.Robot.Worker.Strategy.Estrategias.PriceActionBarByBar());
        catalogo.Registrar(new Financial.Robot.Worker.Strategy.Interpretada.EstrategiaInterpretada());
        return catalogo;
    });

    builder.Services.AddSingleton<Financial.Robot.Worker.Risk.RiskGuard>();
    builder.Services.AddSingleton<Financial.Robot.Worker.Risk.CalculadoraLote>();
    builder.Services.AddSingleton<Financial.Robot.Application.Interfaces.IServicoExecucao, Financial.Robot.Worker.Execution.ServicoExecucao>();
    builder.Services.AddHostedService<Financial.Robot.Worker.Strategy.StrategyEngineFactory>();

    // ─── Fase 8: Gestão Dinâmica de Posições (Breakeven + Trailing) ──────────
    builder.Services.AddHostedService<Financial.Robot.Worker.Execution.GerenciadorPosicoesAbertasService>();

    // ─── Fase 9: Alvo Global sobre Posições Abertas (fecha tudo ao cruzar meta/stop) ──
    builder.Services.AddHostedService<Financial.Robot.Worker.Risk.AlvoGlobalPosicoesAbertasService>();

    // ─── Worker ─────────────────────────────────────────────────────────────
    builder.Services.AddHostedService<Financial.Robot.Worker.Workers.TradingWorker>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host encerrado de forma inesperada.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
