---
name: clean-code
description: |
  Práticas de Clean Code para .NET/C#. Use esta skill ao escrever, revisar
  ou refatorar código para garantir legibilidade, manutenibilidade e
  expressividade. Cobre nomenclatura, funções, classes, comentários,
  tratamento de erros, formatação e práticas idiomáticas de C# moderno.
---

# Clean Code para .NET/C#

## Quando Usar
- Escrever novo código (qualquer camada)
- Revisar ou refatorar código existente
- Nomear variáveis, métodos, classes, namespaces
- Decidir sobre tratamento de erros
- Estruturar e formatar código

---

## 1. Nomenclatura

### Regras Obrigatórias
- **Classes/Records/Structs**: `PascalCase` — substantivos. Ex: `TradeOrder`, `AccountBalance`
- **Interfaces**: `IPascalCase`. Ex: `ITradeRepository`, `IMt5Gateway`
- **Métodos**: `PascalCase` — verbos. Ex: `ExecuteTrade()`, `CalculateRisk()`
- **Métodos async**: Sufixo `Async`. Ex: `GetBalanceAsync()`
- **Variáveis locais/parâmetros**: `camelCase`. Ex: `tradeVolume`, `accountId`
- **Campos privados**: `_camelCase`. Ex: `_repository`, `_logger`
- **Constantes**: `PascalCase`. Ex: `MaxRetryCount`, `DefaultTimeout`
- **Propriedades**: `PascalCase`. Ex: `TotalProfit`, `IsActive`
- **Enums**: `PascalCase` singular. Ex: `OrderType`, `TradeStatus`
- **Booleanos**: Prefixos `Is`, `Has`, `Can`, `Should`. Ex: `IsActive`, `HasOpenPosition`

### Regras de Expressividade
- Nomes devem revelar intenção: `elapsedTimeInMs` > `t`
- Nomes devem ser pronunciáveis: `customerAddress` > `cstmrAddr`
- Nomes buscáveis: evitar single-letter variables (exceto loops curtos)
- Sem prefixos húngaros: `accountList` > `lstAccount`
- Sem abreviações obscuras: `repository` > `repo` (exceto convenções consagradas)

---

## 2. Funções / Métodos

### Regras
- **Tamanho máximo**: 20 linhas (ideal: 5-10)
- **Parâmetros**: máximo 3 (usar objeto para mais)
- **Nível de abstração único**: cada método opera em um só nível
- **Sem side effects escondidos**: o nome deve refletir tudo que o método faz
- **Command-Query Separation**: métodos ou retornam dados OU alteram estado, não ambos
- **Early return**: prefira guard clauses a aninhamento profundo

### ❌ Ruim
```csharp
public async Task<Result> Process(string s, int t, bool f, decimal a, string c)
{
    if (s != null)
    {
        if (t > 0)
        {
            if (f)
            {
                // 3 níveis de aninhamento, parâmetros incompreensíveis
                var x = a * 0.01m;
                // ... 40 linhas de lógica misturada
            }
        }
    }
    return Result.Fail("Invalid");
}
```

### ✅ Limpo
```csharp
public async Task<Result> ExecuteTradeAsync(
    TradeRequest request,
    CancellationToken ct = default)
{
    if (!request.IsValid())
        return Result.Fail("Invalid trade request.");

    var trade = Trade.Create(request.Symbol, request.Volume, request.Price);
    var fee = _feeCalculator.Calculate(trade);

    trade.ApplyFee(fee);
    await _repository.AddAsync(trade, ct);

    return Result.Ok(trade.Id);
}
```

---

## 3. Classes

### Regras
- **Tamanho máximo**: 200 linhas
- **Coesão alta**: todos os membros se relacionam com a responsabilidade central
- **Dependências via construtor**: máximo 4-5 dependências injetadas
- **Seladas por padrão**: usar `sealed` se não for projetada para herança
- **Sem membros estáticos mutáveis**: exceção para singletons thread-safe
- **Dominios Ricos** use sempre dominios ricos
- **Dominio com set privado** em toda classe de dominio use set privado para evitar alterações indesejadas.

### Organização Interna
```csharp
public sealed class TradeService : ITradeService
{
    // 1. Constantes
    private const int MaxRetryCount = 3;

    // 2. Campos privados (readonly)
    private readonly ITradeRepository _repository;
    private readonly ILogger<TradeService> _logger;

    // 3. Construtor
    public TradeService(ITradeRepository repository, ILogger<TradeService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // 4. Propriedades públicas

    // 5. Métodos públicos

    // 6. Métodos privados (auxiliares)
}
```

---

## 4. Tratamento de Erros

### Regras
- **Nunca** catch genérico `catch (Exception)` sem re-throw ou logging
- Usar exceções de domínio tipadas: `InsufficientBalanceException`, `InvalidTradeException`
- Exceções para situações **excepcionais**, não para controle de fluxo
- Usar `Result<T>` pattern para erros esperados de negócio
- `CancellationToken` em todas as operações async

### Result Pattern
```csharp
public sealed record Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(T value) => Value = value;
    private Result(string error) => Error = error;

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error) => new(error);

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}
```

---

## 5. Comentários

### Regras
- **Código deve ser auto-explicativo** — comentários são último recurso
- **Bons comentários**: XML docs em APIs públicas, explicações de "porquê" (não "o quê")
- **Comentários proibidos**: código comentado, comentários redundantes, journal comments
- **TODO**: Aceitável temporariamente com nome e issue tracker ID

### ✅ Bom
```csharp
/// <summary>
/// Calculates the margin requirement for a given position.
/// Uses the leveraged margin formula required by CVM regulation 40/2020.
/// </summary>
public Money CalculateMarginRequirement(Position position)
```

### ❌ Ruim
```csharp
// This method calculates margin
public Money CalculateMarginRequirement(Position position)

// int i = 0; // old code
// i++; // increment i
```

---

## 6. C# Moderno (Idiomático)

### Usar
```csharp
// Pattern matching
if (order is { Status: OrderStatus.Filled, Volume: > 0 } filledOrder)

// Target-typed new
List<Trade> trades = [];
Dictionary<string, decimal> prices = new();

// Null coalescing
var name = user?.Name ?? "Unknown";

// String interpolation
_logger.LogInformation("Trade {TradeId} executed at {Price}", trade.Id, trade.Price);

// Collection expressions (.NET 8+)
int[] numbers = [1, 2, 3, 4, 5];

// Primary constructors (.NET 8+)
public sealed class TradeHandler(ITradeRepository repository, ILogger<TradeHandler> logger)

// Global usings / Implicit usings
// File-scoped namespaces
namespace Financial.Robot.Domain.Entities;

// Records para DTOs e Value Objects
public sealed record TradeDto(Guid Id, string Symbol, decimal Volume, decimal Price);
```

---

## 7. Checklist de Code Review

| Critério | Pergunta |
|----------|----------|
| **Nomes** | Os nomes revelam intenção sem necessidade de comentário? |
| **Funções** | Cada função faz uma única coisa com no máximo 20 linhas? |
| **Classes** | A classe tem uma única responsabilidade com no máximo 200 linhas? |
| **Erros** | Erros de negócio usam Result pattern? Exceções só para casos excepcionais? |
| **DRY** | Existe duplicação que deveria ser abstraída? |
| **KISS** | A solução é a mais simples possível? |
| **Testes** | O código é testável (sem `new` de dependências, sem `static` mutável)? |
