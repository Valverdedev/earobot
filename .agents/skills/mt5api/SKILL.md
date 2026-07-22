---
name: mt5api
description: |
  Integração com MetaTrader 5 (MT5) via API em projetos .NET/C#.
  Use esta skill ao criar, modificar ou debugar código de comunicação
  entre o sistema .NET e o terminal MetaTrader 5, incluindo Named Pipes,
  TCP Sockets, REST API bridges, e o protocolo de comunicação para
  envio de ordens, consulta de posições, dados de mercado e gerenciamento
  de conta. Cobre também a arquitetura do Expert Advisor (EA) no lado MQL5.
---

# Integração MT5 API para .NET

## Quando Usar
- Criar ou modificar comunicação entre .NET e MT5
- Implementar envio de ordens (Market, Limit, Stop)
- Consultar posições abertas, histórico de trades
- Obter dados de mercado em tempo real (ticks, candles)
- Configurar Named Pipes ou TCP Sockets para IPC
- Estruturar o Expert Advisor (EA) no lado MQL5
- Implementar reconexão e resiliência na comunicação

---

## Arquitetura de Comunicação

```
┌─────────────────────────┐         ┌──────────────────────────┐
│     .NET Application    │         │    MetaTrader 5 Terminal  │
│                         │         │                          │
│  ┌───────────────────┐  │  Named  │  ┌────────────────────┐  │
│  │   IMt5Gateway     │──┼──Pipe───┼──│  Expert Advisor    │  │
│  │   (Interface)     │  │   or    │  │  (MQL5)            │  │
│  └───────┬───────────┘  │  TCP    │  │                    │  │
│          │              │  Socket  │  │  ┌──────────────┐  │  │
│  ┌───────▼───────────┐  │         │  │  │ Pipe/Socket  │  │  │
│  │ Mt5PipeGateway    │  │         │  │  │ Server       │  │  │
│  │ (Implementation)  │  │         │  │  └──────────────┘  │  │
│  └───────────────────┘  │         │  └────────────────────┘  │
└─────────────────────────┘         └──────────────────────────┘
```

---

## 1. Contratos (Domain Layer)

### Interface do Gateway MT5
```csharp
namespace Financial.Robot.Domain.Interfaces;

/// <summary>
/// Gateway para comunicação com o terminal MetaTrader 5.
/// Interface definida no Domain, implementada na Infrastructure.
/// </summary>
public interface IMt5Gateway
{
    /// <summary>Verifica se a conexão com MT5 está ativa.</summary>
    Task<bool> IsConnectedAsync(CancellationToken ct = default);

    /// <summary>Envia uma ordem de mercado.</summary>
    Task<TradeResult> SendMarketOrderAsync(
        MarketOrderRequest request, CancellationToken ct = default);

    /// <summary>Envia uma ordem pendente (Limit/Stop).</summary>
    Task<TradeResult> SendPendingOrderAsync(
        PendingOrderRequest request, CancellationToken ct = default);

    /// <summary>Modifica uma ordem existente (SL/TP).</summary>
    Task<TradeResult> ModifyOrderAsync(
        ModifyOrderRequest request, CancellationToken ct = default);

    /// <summary>Fecha uma posição aberta.</summary>
    Task<TradeResult> ClosePositionAsync(
        long positionTicket, CancellationToken ct = default);

    /// <summary>Obtém todas as posições abertas.</summary>
    Task<IReadOnlyList<Mt5Position>> GetOpenPositionsAsync(
        CancellationToken ct = default);

    /// <summary>Obtém informações da conta.</summary>
    Task<Mt5AccountInfo> GetAccountInfoAsync(
        CancellationToken ct = default);

    /// <summary>Obtém o preço atual de um símbolo.</summary>
    Task<Mt5Tick> GetLastTickAsync(
        string symbol, CancellationToken ct = default);

    /// <summary>Obtém dados de candles (OHLCV).</summary>
    Task<IReadOnlyList<Mt5Candle>> GetCandlesAsync(
        string symbol, Mt5Timeframe timeframe, int count,
        CancellationToken ct = default);
}
```

### Modelos do Domínio para MT5
```csharp
namespace Financial.Robot.Domain.ValueObjects;

public sealed record MarketOrderRequest(
    string Symbol,
    OrderSide Side,
    decimal Volume,
    decimal? StopLoss = null,
    decimal? TakeProfit = null,
    string? Comment = null);

public sealed record PendingOrderRequest(
    string Symbol,
    OrderSide Side,
    PendingOrderType Type,
    decimal Price,
    decimal Volume,
    decimal? StopLoss = null,
    decimal? TakeProfit = null,
    DateTime? Expiration = null);

public sealed record ModifyOrderRequest(
    long Ticket,
    decimal? NewStopLoss = null,
    decimal? NewTakeProfit = null,
    decimal? NewPrice = null);

public sealed record TradeResult(
    bool Success,
    long Ticket,
    decimal ExecutedPrice,
    decimal ExecutedVolume,
    string? ErrorMessage = null)
{
    public static TradeResult Failed(string error) =>
        new(false, 0, 0, 0, error);
}

public sealed record Mt5Position(
    long Ticket,
    string Symbol,
    OrderSide Side,
    decimal Volume,
    decimal OpenPrice,
    decimal CurrentPrice,
    decimal Profit,
    decimal? StopLoss,
    decimal? TakeProfit,
    DateTime OpenTime);

public sealed record Mt5AccountInfo(
    long Login,
    string Server,
    string Name,
    decimal Balance,
    decimal Equity,
    decimal FreeMargin,
    decimal MarginLevel,
    string Currency);

public sealed record Mt5Tick(
    string Symbol,
    decimal Bid,
    decimal Ask,
    decimal Last,
    decimal Volume,
    DateTime Time);

public sealed record Mt5Candle(
    DateTime Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long TickVolume,
    long RealVolume,
    int Spread);

public enum OrderSide { Buy, Sell }
public enum PendingOrderType { BuyLimit, SellLimit, BuyStop, SellStop }
public enum Mt5Timeframe
{
    M1 = 1, M5 = 5, M15 = 15, M30 = 30,
    H1 = 60, H4 = 240, D1 = 1440, W1 = 10080, MN1 = 43200
}
```

---

## 2. Protocolo de Comunicação (JSON sobre Pipes/Sockets)

### Formato de Mensagem
```json
{
  "action": "MARKET_ORDER",
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2025-01-15T10:30:00Z",
  "payload": {
    "symbol": "EURUSD",
    "side": "BUY",
    "volume": 0.1,
    "stopLoss": 1.0850,
    "takeProfit": 1.0950,
    "comment": "EA_Signal_001"
  }
}
```

### Ações Suportadas
| Action | Descrição |
|--------|-----------|
| `MARKET_ORDER` | Ordem a mercado |
| `PENDING_ORDER` | Ordem pendente |
| `MODIFY_ORDER` | Modificar SL/TP/Preço |
| `CLOSE_POSITION` | Fechar posição |
| `GET_POSITIONS` | Listar posições abertas |
| `GET_ACCOUNT` | Info da conta |
| `GET_TICK` | Último tick de um símbolo |
| `GET_CANDLES` | Dados OHLCV |
| `HEARTBEAT` | Keep-alive |

### Formato de Resposta
```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "success": true,
  "data": { ... },
  "error": null
}
```

---

## 3. Implementação .NET (Infrastructure Layer)

### Named Pipe Gateway
```csharp
namespace Financial.Robot.Infrastructure.ExternalServices.Mt5;

public sealed class Mt5PipeGateway : IMt5Gateway, IAsyncDisposable
{
    private readonly Mt5ConnectionOptions _options;
    private readonly ILogger<Mt5PipeGateway> _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private NamedPipeClientStream? _pipeClient;

    public Mt5PipeGateway(
        IOptions<Mt5ConnectionOptions> options,
        ILogger<Mt5PipeGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await SendRequestAsync<HeartbeatResponse>(
                new Mt5Request("HEARTBEAT"), ct);
            return response.Success;
        }
        catch
        {
            return false;
        }
    }

    public async Task<TradeResult> SendMarketOrderAsync(
        MarketOrderRequest request,
        CancellationToken ct = default)
    {
        var mt5Request = new Mt5Request("MARKET_ORDER", new
        {
            symbol = request.Symbol,
            side = request.Side.ToString().ToUpperInvariant(),
            volume = request.Volume,
            stopLoss = request.StopLoss,
            takeProfit = request.TakeProfit,
            comment = request.Comment
        });

        var response = await SendRequestAsync<Mt5TradeResponse>(mt5Request, ct);

        return response.Success
            ? new TradeResult(true, response.Data!.Ticket,
                response.Data.Price, response.Data.Volume)
            : TradeResult.Failed(response.Error ?? "Unknown MT5 error");
    }

    private async Task<Mt5Response<T>> SendRequestAsync<T>(
        Mt5Request request,
        CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            await EnsureConnectedAsync(ct);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var buffer = Encoding.UTF8.GetBytes(json + "\n");

            await _pipeClient!.WriteAsync(buffer, ct);
            await _pipeClient.FlushAsync(ct);

            var responseJson = await ReadResponseAsync(ct);

            return JsonSerializer.Deserialize<Mt5Response<T>>(
                responseJson, _jsonOptions)
                ?? throw new Mt5CommunicationException("Null response from MT5");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "MT5 pipe communication error");
            await ReconnectAsync(ct);
            throw new Mt5CommunicationException("Lost connection to MT5", ex);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_pipeClient is { IsConnected: true })
            return;

        _pipeClient?.Dispose();
        _pipeClient = new NamedPipeClientStream(
            _options.ServerName,
            _options.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await _pipeClient.ConnectAsync(
            _options.ConnectionTimeoutMs, ct);

        _logger.LogInformation(
            "Connected to MT5 pipe: {PipeName}", _options.PipeName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_pipeClient is not null)
        {
            await _pipeClient.DisposeAsync();
        }
        _sendLock.Dispose();
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
```

### Configuração
```csharp
public sealed record Mt5ConnectionOptions
{
    public const string SectionName = "Mt5Connection";

    public string ServerName { get; init; } = ".";
    public string PipeName { get; init; } = "financial_robot_mt5";
    public int ConnectionTimeoutMs { get; init; } = 5000;
    public int ReadTimeoutMs { get; init; } = 10000;
    public int ReconnectDelayMs { get; init; } = 3000;
    public int MaxRetryAttempts { get; init; } = 3;
}
```

### Registro de DI
```csharp
public static class Mt5ServiceExtensions
{
    public static IServiceCollection AddMt5Integration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<Mt5ConnectionOptions>(
            configuration.GetSection(Mt5ConnectionOptions.SectionName));

        services.AddSingleton<IMt5Gateway, Mt5PipeGateway>();

        return services;
    }
}
```

---

## 4. Expert Advisor (EA) — Lado MQL5

### Estrutura Básica do EA Servidor
```mql5
// EA que atua como servidor de Named Pipe / Socket
// Recebe comandos JSON do .NET e executa no MT5

#property copyright "Financial Robot"
#property version   "1.00"
#property strict

#include <Trade\Trade.mqh>
#include <Files\FilePipe.mqh>  // ou implementação custom

input string PipeName = "financial_robot_mt5";  // Nome do pipe

CFilePipe pipe;
CTrade    trade;

int OnInit()
{
    if (!pipe.Open(PipeName, FILE_READ | FILE_WRITE | FILE_BIN))
    {
        PrintFormat("Failed to open pipe: %s", PipeName);
        return INIT_FAILED;
    }
    PrintFormat("Pipe server started: %s", PipeName);
    return INIT_SUCCEEDED;
}

void OnTick()
{
    // Verificar se há mensagem no pipe
    string request = pipe.ReadString();
    if (StringLen(request) > 0)
    {
        string response = ProcessRequest(request);
        pipe.WriteString(response);
    }
}

void OnDeinit(const int reason)
{
    pipe.Close();
    PrintFormat("Pipe server stopped. Reason: %d", reason);
}
```

---

## 5. Resiliência e Boas Práticas

### Políticas com Polly
```csharp
services.AddSingleton<IMt5Gateway>(sp =>
{
    var gateway = new Mt5PipeGateway(
        sp.GetRequiredService<IOptions<Mt5ConnectionOptions>>(),
        sp.GetRequiredService<ILogger<Mt5PipeGateway>>());

    // Wrap com retry + circuit breaker via Polly
    var retryPolicy = Policy
        .Handle<Mt5CommunicationException>()
        .WaitAndRetryAsync(3,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    var circuitBreaker = Policy
        .Handle<Mt5CommunicationException>()
        .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));

    // Retornar decorator com políticas
    return new ResilientMt5Gateway(gateway, retryPolicy, circuitBreaker);
});
```

### Checklist de Segurança MT5
| Item | Verificação |
|------|------------|
| **Validação** | Todos os inputs são validados antes de enviar ao MT5 |
| **Volume** | Volume máximo por ordem está configurado |
| **Drawdown** | Limite de drawdown diário está implementado |
| **Conexão** | Reconexão automática com backoff exponencial |
| **Logging** | Todas as ordens são logadas com correlation ID |
| **Heartbeat** | Health check periódico da conexão MT5 |
| **Circuit Breaker** | Parada automática após N falhas consecutivas |
| **Timeout** | Timeout configurado para todas as operações |

---

## 6. appsettings.json
```json
{
  "Mt5Connection": {
    "ServerName": ".",
    "PipeName": "financial_robot_mt5",
    "ConnectionTimeoutMs": 5000,
    "ReadTimeoutMs": 10000,
    "ReconnectDelayMs": 3000,
    "MaxRetryAttempts": 3
  }
}
```
