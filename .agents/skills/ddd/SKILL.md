---
name: ddd
description: |
  Domain-Driven Design (DDD) skill para projetos .NET/C#.
  Use esta skill quando precisar criar, modificar ou refatorar código seguindo
  os padrões táticos e estratégicos do DDD. Inclui orientações para Entities,
  Value Objects, Aggregates, Domain Events, Repositories, Domain Services,
  Application Services e Bounded Contexts.
---

# Domain-Driven Design (DDD) para .NET

## Quando Usar
- Criar novas entidades, value objects ou aggregates
- Definir bounded contexts e context maps
- Implementar domain events e event handlers
- Criar repositories e specifications
- Refatorar código anêmico para modelo rico de domínio
- Estruturar camadas da aplicação seguindo Clean Architecture + DDD

## Estrutura de Camadas

```
src/
├── Domain/                          # Núcleo do domínio (ZERO dependências externas)
│   ├── Entities/                    # Entidades com identidade
│   ├── ValueObjects/                # Objetos de valor (imutáveis)
│   ├── Aggregates/                  # Raízes de agregado
│   ├── Events/                      # Domain Events
│   ├── Exceptions/                  # Exceções do domínio
│   ├── Interfaces/                  # Contratos (IRepository, IDomainService)
│   ├── Services/                    # Domain Services (lógica que não pertence a uma entidade)
│   ├── Specifications/             # Specification Pattern
│   └── Enums/                      # Enumerações do domínio
│
├── Application/                     # Casos de uso / orquestração
│   ├── Commands/                    # CQRS Commands + Handlers
│   ├── Queries/                     # CQRS Queries + Handlers
│   ├── DTOs/                        # Data Transfer Objects
│   ├── Interfaces/                  # Contratos de Application Services
│   ├── Services/                    # Application Services
│   ├── Mappers/                     # Mapeamento Domain <-> DTO
│   ├── Validators/                  # FluentValidation validators
│   └── Behaviors/                   # MediatR Pipeline Behaviors
│
├── Infrastructure/                  # Implementações externas
│   ├── Persistence/                 # EF Core DbContext, Configurations
│   │   ├── Configurations/          # IEntityTypeConfiguration<T>
│   │   ├── Repositories/           # Implementações dos repositories
│   │   └── Migrations/             # EF Core Migrations
│   ├── ExternalServices/           # Integrações externas (MT5, APIs)
│   ├── Messaging/                  # Event Bus, Message Broker
│   └── DependencyInjection/        # Registro de serviços
│
└── API/                            # Apresentação (Controllers, Endpoints)
    ├── Controllers/
    ├── Middleware/
    ├── Filters/
    └── Extensions/
```

## Regras Fundamentais

### 1. Entity (Entidade)
- DEVE ter identidade única (Id)
- DEVE herdar de `Entity<TId>` base class
- Igualdade baseada no Id, não nos atributos
- Encapsula lógica de negócio relacionada
- Estado mutável, mas controlado via métodos

```csharp
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected init; }

    protected Entity(TId id) => Id = id;

    // Construtor para EF Core
    protected Entity() { }

    public bool Equals(Entity<TId>? other) =>
        other is not null && Id.Equals(other.Id);

    public override bool Equals(object? obj) =>
        obj is Entity<TId> entity && Equals(entity);

    public override int GetHashCode() => Id.GetHashCode();
}
```

### 2. Value Object
- DEVE ser imutável (usar `record`)
- Igualdade baseada nos atributos
- Sem identidade própria
- Auto-validante no construtor

```csharp
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new DomainException("Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Currency is required.");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("Cannot add different currencies.");
        return new Money(Amount + other.Amount, Currency);
    }

    public static Money Zero(string currency) => new(0, currency);
}
```

### 3. Aggregate Root
- DEVE herdar de `AggregateRoot<TId>`
- É a única porta de entrada para modificar o agregado
- Garante invariantes de negócio
- Publica Domain Events
- Repositório existe apenas para Aggregate Roots

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents =>
        _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected AggregateRoot(TId id) : base(id) { }
    protected AggregateRoot() { }
}
```

### 4. Domain Events
- Representam algo que **aconteceu** no domínio (passado)
- Nomeados no passado: `OrderPlaced`, `TradeExecuted`
- Imutáveis (usar `record`)

```csharp
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

public sealed record TradeExecutedEvent(
    Guid TradeId,
    string Symbol,
    decimal Volume,
    decimal Price,
    DateTime OccurredOn) : IDomainEvent;
```

### 5. Repository Pattern
- Interface no **Domain**, implementação na **Infrastructure**
- Um repositório por Aggregate Root
- Não expor IQueryable (encapsular queries)

```csharp
// Domain/Interfaces/
public interface ITradeRepository
{
    Task<Trade?> GetByIdAsync(TradeId id, CancellationToken ct = default);
    Task<IReadOnlyList<Trade>> GetBySymbolAsync(string symbol, CancellationToken ct = default);
    Task AddAsync(Trade trade, CancellationToken ct = default);
    Task UpdateAsync(Trade trade, CancellationToken ct = default);
}
```

### 6. Domain Service
- Lógica que não pertence naturalmente a uma única entidade
- Stateless
- Opera sobre múltiplas entidades/agregados

```csharp
public sealed class RiskCalculationService : IDomainService
{
    public RiskAssessment CalculatePositionRisk(
        IReadOnlyList<Trade> openTrades,
        Money accountBalance)
    {
        // Lógica de cálculo de risco que envolve múltiplos trades
    }
}
```

### 7. Specification Pattern
- Encapsula regras de consulta reutilizáveis

```csharp
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T entity) =>
        ToExpression().Compile()(entity);
}
```

## Anti-Patterns a Evitar
1. **Modelo Anêmico**: Entidades sem comportamento (apenas getters/setters)
2. **Repositório Genérico Puro**: `IRepository<T>` sem métodos específicos do domínio
3. **Domain Event como Command**: Events são fatos passados, não ordens
4. **Aggregate muito grande**: Prefira aggregates pequenos e coesos
5. **Lógica de negócio no Application Service**: Mova para o Domain
6. **Dependências externas no Domain**: O Domain NÃO referencia Infrastructure
7. **Expor IQueryable**: Isso vaza implementação para fora do repositório
