using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Financial.Robot.Worker.Config;

public class ConfigWatcherService : BackgroundService
{
    private readonly ILogger<ConfigWatcherService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConfigValidator _validator;
    private readonly IConnectionManager _connectionManager;
    private FileSystemWatcher? _watcher;

    // Armazena as configurações ativas por símbolo
    private readonly ConcurrentDictionary<string, SymbolConfig> _activeConfigs = new();

    // Armazena o conteúdo do último json processado por arquivo para evitar recarregamento desnecessário
    private readonly ConcurrentDictionary<string, string> _lastJsonContent = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Disparado sempre que uma configuração válida é carregada ou atualizada.</summary>
    public event Action<SymbolConfig>? ConfigChanged;

    // Controle de debounce
    private readonly ConcurrentDictionary<string, Timer> _debouncers = new();
    private readonly TimeSpan _debounceTime = TimeSpan.FromMilliseconds(500);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigWatcherService(
        ILogger<ConfigWatcherService> logger,
        IConfiguration configuration,
        ConfigValidator validator,
        IConnectionManager connectionManager)
    {
        _logger = logger;
        _configuration = configuration;
        _validator = validator;
        _connectionManager = connectionManager;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configPath = _configuration["ConfigWatcher:Path"] ?? "config";

        var fullPath = Path.GetFullPath(configPath);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
            _logger.LogInformation("Diretório de configurações criado em {Path}", fullPath);
        }

        _logger.LogInformation("Iniciando ConfigWatcher em {Path}", fullPath);

        _watcher = new FileSystemWatcher(fullPath, "*.config.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileChanged;

        // Carrega configurações iniciais e faz polling periódico (fallback seguro caso o watcher perca eventos)
        Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            do
            {
                try
                {
                    foreach (var file in Directory.GetFiles(fullPath, "*.config.json"))
                    {
                        await ProcessFileAsync(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro no polling de configurações");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }, stoppingToken);

        return Task.CompletedTask;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce para evitar múltiplos eventos do FileSystemWatcher
        _debouncers.AddOrUpdate(e.FullPath,
            key => new Timer(_ => Task.Run(async () => await ProcessFileAsync(key)), null, _debounceTime, Timeout.InfiniteTimeSpan),
            (key, oldTimer) =>
            {
                oldTimer.Change(_debounceTime, Timeout.InfiniteTimeSpan);
                return oldTimer;
            });
    }

    private async Task ProcessFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            string json = File.ReadAllText(filePath);

            // Se o conteúdo JSON for idêntico ao último processado, ignora (evita falsos positivos de alteração no polling)
            if (_lastJsonContent.TryGetValue(filePath, out var oldJson) && oldJson == json)
                return;

            var config = JsonSerializer.Deserialize<SymbolConfig>(json, _jsonOptions);

            if (config != null)
            {
                if (await _validator.IsValidAsync(config))
                {
                    _lastJsonContent[filePath] = json;

                    var configKey = CriarConfigKey(config);

                    _activeConfigs.AddOrUpdate(configKey,
                        key =>
                        {
                            _logger.LogInformation("Novo config carregado para {Symbol}", config.Symbol);
                            ConfigChanged?.Invoke(config);
                            return config;
                        },
                        (key, oldConfig) =>
                        {
                            _logger.LogInformation("Config atualizado para {Symbol}", config.Symbol);
                            ConfigChanged?.Invoke(config);
                            return config;
                        });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar arquivo de configuração {File}", Path.GetFileName(filePath));
        }
    }

    public IEnumerable<SymbolConfig> GetActiveConfigs()
    {
        return _activeConfigs.Values;
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        foreach (var timer in _debouncers.Values)
        {
            timer.Dispose();
        }
        base.Dispose();
    }

    private static string CriarConfigKey(SymbolConfig config)
    {
        var brokerSymbol = config.BrokerSymbol ?? config.Symbol;
        return $"{config.TerminalId}:{config.Symbol}:{brokerSymbol}";
    }
}
