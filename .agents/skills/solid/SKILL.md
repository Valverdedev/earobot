---
name: solid
description: |
  Princípios SOLID para desenvolvimento .NET/C#.
  Use esta skill ao criar, revisar ou refatorar classes e interfaces,
  garantindo que o código siga os cinco princípios SOLID:
  Single Responsibility, Open/Closed, Liskov Substitution,
  Interface Segregation e Dependency Inversion.
---

# Princípios SOLID para .NET/C#

## Quando Usar
- Criar novas classes ou interfaces
- Refatorar classes com múltiplas responsabilidades
- Revisar code design e acoplamento
- Decidir sobre abstrações e hierarquias de herança
- Aplicar injeção de dependência

---

## S — Single Responsibility Principle (SRP)

> Uma classe deve ter apenas **um motivo para mudar**.

### Regras
- Cada classe deve ter uma única responsabilidade clara
- Se o nome da classe contém "And", "Manager", "Handler" genérico ou "Util", provavelmente viola SRP
- Métodos privados excessivos indicam responsabilidades escondidas
- Limite: **máximo 200 linhas** por classe, **máximo 20 linhas** por método

### ❌ Violação
```csharp
// Esta classe faz TUDO: valida, persiste, notifica
public class TradeService
{
    public async Task ExecuteTrade(TradeRequest request)
    {
        // Validação
        if (request.Volume <= 0) throw new Exception("Invalid volume");
        if (string.IsNullOrEmpty(request.Symbol)) throw new Exception("Invalid symbol");

        // Persistência
        using var connection = new SqlConnection("...");
        await connection.ExecuteAsync("INSERT INTO Trades ...", request);

        // Notificação
        var client = new SmtpClient();
        await client.SendMailAsync(new MailMessage(...));
    }
}
```

### ✅ Correto
```csharp
public sealed class TradeValidator : AbstractValidator<TradeRequest>
{
    public TradeValidator()
    {
        RuleFor(x => x.Volume).GreaterThan(0);
        RuleFor(x => x.Symbol).NotEmpty();
    }
}

public sealed class TradeCommandHandler : IRequestHandler<ExecuteTradeCommand, TradeResult>
{
    private readonly ITradeRepository _repository;
    private readonly INotificationService _notifications;

    public TradeCommandHandler(ITradeRepository repository, INotificationService notifications)
    {
        _repository = repository;
        _notifications = notifications;
    }

    public async Task<TradeResult> Handle(ExecuteTradeCommand command, CancellationToken ct)
    {
        var trade = Trade.Create(command.Symbol, command.Volume, command.Price);
        await _repository.AddAsync(trade, ct);
        await _notifications.NotifyTradeExecutedAsync(trade, ct);
        return TradeResult.From(trade);
    }
}
```

---

## O — Open/Closed Principle (OCP)

> Classes devem estar **abertas para extensão** e **fechadas para modificação**.

### Regras
- Usar abstrações (interfaces, classes abstratas) para pontos de extensão
- Preferir composição sobre herança
- Usar Strategy Pattern, Template Method ou Decorator para extensibilidade
- Novos comportamentos = novas classes, não `if/else` em classes existentes

### ❌ Violação
```csharp
public decimal CalculateFee(string orderType, decimal amount)
{
    // Cada novo tipo = modificar este método
    return orderType switch
    {
        "Market" => amount * 0.001m,
        "Limit" => amount * 0.0008m,
        "StopLoss" => amount * 0.0012m,
        _ => throw new ArgumentException("Unknown order type")
    };
}
```

### ✅ Correto
```csharp
public interface IFeeCalculator
{
    string OrderType { get; }
    decimal Calculate(decimal amount);
}

public sealed class MarketFeeCalculator : IFeeCalculator
{
    public string OrderType => "Market";
    public decimal Calculate(decimal amount) => amount * 0.001m;
}

public sealed class FeeCalculatorFactory
{
    private readonly IReadOnlyDictionary<string, IFeeCalculator> _calculators;

    public FeeCalculatorFactory(IEnumerable<IFeeCalculator> calculators)
    {
        _calculators = calculators.ToDictionary(c => c.OrderType);
    }

    public IFeeCalculator GetCalculator(string orderType) =>
        _calculators.TryGetValue(orderType, out var calc)
            ? calc
            : throw new DomainException($"No calculator for order type: {orderType}");
}
```

---

## L — Liskov Substitution Principle (LSP)

> Subtipos devem ser substituíveis por seus tipos base **sem alterar o comportamento correto** do programa.

### Regras
- Subclasses não devem lançar exceções inesperadas que o tipo base não lançaria
- Pré-condições não podem ser fortalecidas no subtipo
- Pós-condições não podem ser enfraquecidas no subtipo
- Invariantes do tipo base devem ser preservadas
- Se usar `throw new NotSupportedException()` em um override, há violação de LSP

### ❌ Violação
```csharp
public class ReadOnlyRepository : ITradeRepository
{
    // Violação: lança exceção onde o contrato espera persistência
    public Task AddAsync(Trade trade, CancellationToken ct) =>
        throw new NotSupportedException("This repository is read-only!");
}
```

### ✅ Correto
```csharp
// Separe as interfaces conforme a capacidade real
public interface ITradeReader
{
    Task<Trade?> GetByIdAsync(TradeId id, CancellationToken ct = default);
}

public interface ITradeWriter
{
    Task AddAsync(Trade trade, CancellationToken ct = default);
}

public interface ITradeRepository : ITradeReader, ITradeWriter { }

// Agora ReadOnly implementa apenas o que suporta
public sealed class ReadOnlyTradeRepository : ITradeReader { ... }
```

---

## I — Interface Segregation Principle (ISP)

> Clientes não devem ser forçados a depender de métodos que **não utilizam**.

### Regras
- Interfaces devem ser pequenas e focadas (max 5 métodos como guideline)
- Prefira múltiplas interfaces coesas a uma interface "god"
- Nomeie interfaces pelo que o **consumidor** precisa, não pelo que o **implementador** faz
- Se uma classe implementa uma interface mas deixa métodos vazios/throw, viola ISP

### ❌ Violação
```csharp
public interface ITradeService
{
    Task<Trade> OpenTrade(TradeRequest request);
    Task CloseTrade(TradeId id);
    Task<IEnumerable<Trade>> GetTradeHistory();
    Task<AccountBalance> GetBalance();          // Não é responsabilidade de trade
    Task SendNotification(string message);      // Notificação != Trade
    Task GenerateReport(ReportParams p);        // Relatório != Trade
}
```

### ✅ Correto
```csharp
public interface ITradeExecutor
{
    Task<Trade> OpenTradeAsync(TradeRequest request, CancellationToken ct = default);
    Task CloseTradeAsync(TradeId id, CancellationToken ct = default);
}

public interface ITradeQueryService
{
    Task<IReadOnlyList<Trade>> GetTradeHistoryAsync(CancellationToken ct = default);
}

public interface IAccountService
{
    Task<AccountBalance> GetBalanceAsync(CancellationToken ct = default);
}
```

---

## D — Dependency Inversion Principle (DIP)

> Módulos de alto nível não devem depender de módulos de baixo nível. **Ambos devem depender de abstrações**.

### Regras
- **Sempre** usar constructor injection
- Interfaces definidas no **consumidor** (Domain/Application), implementadas no **provedor** (Infrastructure)
- Registrar dependências no Composition Root (`Program.cs` / `DependencyInjection/`)
- Nunca instanciar dependências com `new` dentro de classes de negócio
- Usar `IOptions<T>` para configurações

### ❌ Violação
```csharp
public class TradeExecutor
{
    public async Task Execute()
    {
        // Dependência direta de implementação concreta
        var mt5Client = new Mt5TcpClient("localhost", 5555);
        var result = await mt5Client.SendOrderAsync(...);

        var logger = new FileLogger("trades.log");
        logger.Log(result.ToString());
    }
}
```

### ✅ Correto
```csharp
public sealed class TradeExecutor : ITradeExecutor
{
    private readonly IMt5Gateway _mt5Gateway;
    private readonly ILogger<TradeExecutor> _logger;

    public TradeExecutor(IMt5Gateway mt5Gateway, ILogger<TradeExecutor> logger)
    {
        _mt5Gateway = mt5Gateway;
        _logger = logger;
    }

    public async Task<TradeResult> ExecuteAsync(
        TradeCommand command,
        CancellationToken ct = default)
    {
        var result = await _mt5Gateway.SendOrderAsync(command.ToMt5Order(), ct);
        _logger.LogInformation("Trade executed: {TradeId}", result.TradeId);
        return TradeResult.From(result);
    }
}
```

---

## Checklist de Revisão SOLID

| Princípio | Pergunta de Validação |
|-----------|----------------------|
| **SRP** | A classe tem apenas um motivo para mudar? |
| **OCP** | Posso adicionar novo comportamento sem modificar código existente? |
| **LSP** | Posso substituir o subtipo pelo tipo base sem quebrar nada? |
| **ISP** | Todos os métodos da interface são usados por todos os consumidores? |
| **DIP** | As dependências são injetadas via abstração no construtor? |
