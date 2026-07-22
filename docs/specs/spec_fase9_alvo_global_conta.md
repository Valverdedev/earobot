# Fase 9 — Alvo Global de Lucro/Prejuízo (posições abertas)

## Contexto e motivação

Hoje existem limites de drawdown por estratégia (`GestaoRiscoConfig.DrawdownDiarioMaximoPercent`, isolado por magic number) e um limite agregado por símbolo (`SymbolConfig.DrawdownDiarioMaximoAgregadoPercent`). Ambos são checados em `RiskGuard` e só **bloqueiam novas entradas** — nunca fecham posições já abertas — e não existe nenhum equivalente do lado do lucro.

Pedido do usuário (12/07/2026): um alvo global sobre o **resultado flutuante somado de todas as posições abertas, em todos os símbolos/estratégias**. Ao cruzar um teto de lucro ou um piso de prejuízo, fecha **todas** as posições abertas naquele momento (inclusive as que estão no negativo).

**Correção importante feita pelo usuário depois da primeira versão desta spec**: isto NÃO é um circuit breaker diário que pausa o robô. É só sobre as ordens que estão abertas agora. Assim que elas são fechadas pelo gatilho, o EA volta a operar normalmente — pode abrir novas posições imediatamente, sem nenhuma trava ou período de espera. Se essas novas posições, por sua vez, acumularem lucro/prejuízo suficiente para cruzar o alvo de novo, o mecanismo dispara de novo, fecha de novo, e assim por diante. Não há "reset diário", não há "pausa", não há estado compartilhado com o `StrategyEngine` — o mecanismo é totalmente independente e não interfere em nenhuma decisão de entrada.

Isso simplifica bastante o desenho em relação à primeira versão desta spec (que tinha `EstadoGestaoGlobal`, pausa até o próximo dia UTC, alterações no `RiskGuard`/`StrategyEngine`/`StrategyEngineFactory`) — **nada disso é necessário agora**. O serviço só lê posições abertas e fecha quando necessário; não tem nenhum outro ponto de contato com o resto do sistema.

## Onde configurar

Configuração de conta, não de símbolo — vai em `appsettings.json`, fora da pasta `config/` (que só tem arquivos `*.config.json` por símbolo, lidos como `SymbolConfig`):

```json
{
  "AlvoGlobalPosicoesAbertas": {
    "Ativo": false,
    "TerminalId": "activtraders",
    "MetaLucroGlobalPercent": null,
    "StopPerdaGlobalPercent": null,
    "IntervaloVerificacaoSegundos": 10
  }
}
```

- `Ativo`: interruptor geral — liga/desliga o mecanismo inteiro. Default `false`.
- `TerminalId`: terminal de execução real a considerar (`activtraders`) — nunca um terminal somente-leitura.
- `MetaLucroGlobalPercent` / `StopPerdaGlobalPercent`: `double?`, percentual sobre o saldo atual da conta. Cada um pode ficar `null` independentemente para desativar só aquele lado (ex.: só fechar no prejuízo, sem teto de lucro). `StopPerdaGlobalPercent` é um número **positivo** (ex.: `2.0` = fecha tudo em -2%); o sinal é aplicado internamente.
- `IntervaloVerificacaoSegundos`: cadência do ciclo (default 10s, mesmo ritmo do `GerenciadorPosicoesAbertasService` da Fase 8).

### `src/Financial.Robot.Application/Configuracoes/ConfiguracaoAlvoGlobalPosicoesAbertas.cs` (novo arquivo)

```csharp
namespace Financial.Robot.Application.Configuracoes;

public sealed class ConfiguracaoAlvoGlobalPosicoesAbertas
{
    public bool Ativo { get; set; } = false;
    public string TerminalId { get; set; } = string.Empty;
    public double? MetaLucroGlobalPercent { get; set; }
    public double? StopPerdaGlobalPercent { get; set; }
    public int IntervaloVerificacaoSegundos { get; set; } = 10;
}
```

Registrar em `Program.cs`, junto às demais configurações tipadas:

```csharp
builder.Services.Configure<Financial.Robot.Application.Configuracoes.ConfiguracaoAlvoGlobalPosicoesAbertas>(
    builder.Configuration.GetSection("AlvoGlobalPosicoesAbertas"));
```

Usar `IOptionsMonitor<T>` (não `IOptions<T>`) no serviço, para permitir ligar/desligar e recalibrar os percentuais editando o `appsettings.json` sem reiniciar o processo (o host recarrega esse arquivo por padrão).

## Novo serviço: `AlvoGlobalPosicoesAbertasService`

Novo arquivo: `src/Financial.Robot.Worker/Risk/AlvoGlobalPosicoesAbertasService.cs`

Roda como `BackgroundService`, totalmente independente — só depende do `ConfigWatcherService` (para saber quais símbolos/estratégias estão ativos) e do `ConnectionManagerService`/`IServicoExecucao` (para ler posições e fechá-las). **Não toca em `RiskGuard`, `StrategyEngine` nem `StrategyEngineFactory`.**

```csharp
using Financial.Robot.Application.Configuracoes;
using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Financial.Robot.Worker.Risk;

/// <summary>
/// Fase 9 — soma o resultado flutuante (não realizado) de TODAS as posições abertas
/// em todos os símbolos/estratégias ativos no terminal de execução. Ao cruzar a meta
/// de lucro ou o stop de perda configurados (em % do saldo atual), fecha todas as
/// posições abertas naquele momento. Não pausa entradas — o EA volta a operar
/// normalmente assim que as posições são fechadas, podendo disparar de novo depois.
/// </summary>
public sealed class AlvoGlobalPosicoesAbertasService : BackgroundService
{
    private readonly IOptionsMonitor<ConfiguracaoAlvoGlobalPosicoesAbertas> _opcoes;
    private readonly ConfigWatcherService _configWatcher;
    private readonly IConnectionManager _connectionManager;
    private readonly IServicoExecucao _execucao;
    private readonly ILogger<AlvoGlobalPosicoesAbertasService> _logger;

    public AlvoGlobalPosicoesAbertasService(
        IOptionsMonitor<ConfiguracaoAlvoGlobalPosicoesAbertas> opcoes,
        ConfigWatcherService configWatcher,
        IConnectionManager connectionManager,
        IServicoExecucao execucao,
        ILogger<AlvoGlobalPosicoesAbertasService> logger)
    {
        _opcoes = opcoes;
        _configWatcher = configWatcher;
        _connectionManager = connectionManager;
        _execucao = execucao;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var cfg = _opcoes.CurrentValue;
            var intervalo = TimeSpan.FromSeconds(Math.Max(5, cfg.IntervaloVerificacaoSegundos));

            try
            {
                if (cfg.Ativo && !string.IsNullOrWhiteSpace(cfg.TerminalId)
                    && (cfg.MetaLucroGlobalPercent is > 0 || cfg.StopPerdaGlobalPercent is > 0))
                {
                    await ProcessarAsync(cfg, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AlvoGlobal] Erro no ciclo de verificação.");
            }

            await Task.Delay(intervalo, stoppingToken);
        }
    }

    private async Task ProcessarAsync(ConfiguracaoAlvoGlobalPosicoesAbertas cfg, CancellationToken ct)
    {
        var gateway = _connectionManager.GetClient(cfg.TerminalId);
        var conta = await gateway.ObterInfoContaAsync(ct);
        if (conta.Saldo <= 0) return;

        // Mapa (symbol, magic) -> lucro flutuante, só das estratégias ativas no terminal de execução.
        var posicoesPorEstrategia = await ListarEstrategiasComLucroAbertoAsync(gateway, cfg.TerminalId, ct);
        if (posicoesPorEstrategia.Count == 0) return; // nada aberto, nada a fazer

        var pnlFlutuanteTotal = posicoesPorEstrategia.Sum(p => p.LucroAberto);
        var pnlPercent = pnlFlutuanteTotal / conta.Saldo * 100.0;

        string? motivo = null;
        if (cfg.MetaLucroGlobalPercent is > 0 && pnlPercent >= cfg.MetaLucroGlobalPercent.Value)
            motivo = $"Meta de lucro global atingida: {pnlPercent:F2}% >= {cfg.MetaLucroGlobalPercent.Value:F2}% (posições abertas)";
        else if (cfg.StopPerdaGlobalPercent is > 0 && pnlPercent <= -cfg.StopPerdaGlobalPercent.Value)
            motivo = $"Stop de perda global atingido: {pnlPercent:F2}% <= -{cfg.StopPerdaGlobalPercent.Value:F2}% (posições abertas)";

        if (motivo is null)
        {
            _logger.LogDebug("[AlvoGlobal] Flutuante atual: {Pnl:F2} ({Percent:F2}% do saldo {Saldo:F2}).", pnlFlutuanteTotal, pnlPercent, conta.Saldo);
            return;
        }

        _logger.LogWarning("[AlvoGlobal] {Motivo} — fechando todas as posições abertas. O EA continua operando normalmente após o fechamento.", motivo);
        await FecharTodasAsync(gateway, cfg.TerminalId, posicoesPorEstrategia, ct);
    }

    private async Task<List<(string Symbol, long Magic, double LucroAberto)>> ListarEstrategiasComLucroAbertoAsync(
        IGatewayMt5 gateway, string terminalId, CancellationToken ct)
    {
        var resultado = new List<(string, long, double)>();

        foreach (var config in _configWatcher.GetActiveConfigs())
        {
            if (config.TerminalId != terminalId) continue; // só o terminal de execução real

            var estrategias = config.Estrategias is { Count: > 0 }
                ? config.Estrategias
                : new List<EstrategiaConfig> { CriarEstrategiaFallback(config) };

            foreach (var est in estrategias.Where(e => e.Ativa))
            {
                var lucroAberto = await gateway.ObterLucroAbertoAsync(config.Symbol, est.MagicNumber, ct);
                if (lucroAberto != 0) // só entra na soma quem tem posição de fato aberta
                    resultado.Add((config.Symbol, est.MagicNumber, lucroAberto));
            }
        }

        return resultado;
    }

    private async Task FecharTodasAsync(
        IGatewayMt5 gateway, string terminalId,
        List<(string Symbol, long Magic, double LucroAberto)> estrategiasComPosicao, CancellationToken ct)
    {
        foreach (var (symbol, magic, _) in estrategiasComPosicao)
        {
            var tickets = await gateway.ObterTicketsPosicoesAbertasAsync(symbol, magic, ct);
            foreach (var ticket in tickets)
            {
                var resultado = await _execucao.FecharPosicaoAsync(terminalId, ticket, ct);
                if (resultado.Sucesso)
                    _logger.LogInformation("[AlvoGlobal] Posição fechada: {Simbolo} Ticket={Ticket} Magic={Magic}", symbol, ticket, magic);
                else
                    _logger.LogWarning("[AlvoGlobal] Falha ao fechar {Simbolo} Ticket={Ticket}: {Erro}", symbol, ticket, resultado.Motivo);
            }
        }
    }

    private static EstrategiaConfig CriarEstrategiaFallback(SymbolConfig config) => new(
        Id: "cruzamento-ema", Nome: "CruzamentoEma", MagicNumber: 1, Ativa: true,
        Comprar: config.Comprar, Vender: config.Vender, Entrada: config.Entrada,
        Saida: config.Saida, GestaoDeRisco: config.GestaoDeRisco,
        JanelaHorarioPermitido: config.GestaoDeRisco?.JanelaHorarioPermitido,
        Indicadores: config.Indicadores, Parametros: new Dictionary<string, object>());
}
```

Pontos de atenção:

- **Só considera resultado flutuante (`ObterLucroAbertoAsync`), nunca P&L fechado do dia.** Não há conceito de "dia" nesta versão — o alvo é sobre o que está aberto agora, ponto. Assim que fecha, o flutuante zera e o ciclo seguinte não encontra nada a fazer, até novas posições abrirem.
- **Sem pausa, sem estado compartilhado, sem tocar em `RiskGuard`/`StrategyEngine`.** O robô pode abrir uma posição nova no ciclo imediatamente seguinte ao fechamento — é o comportamento esperado.
- **Base do percentual é o saldo atual da conta** (`conta.Saldo` do próprio ciclo), não um saldo fixado no início do dia — não faz sentido guardar um "saldo-base do dia" para um mecanismo que não tem noção de dia.
- **Só soma o terminal de execução real** (`cfg.TerminalId`), nunca `broker-genial`.
- **Pode disparar várias vezes no mesmo dia** — é o comportamento pretendido, não um bug: cada vez que o conjunto de posições abertas acumula lucro/prejuízo suficiente, fecha e libera o robô para operar de novo.

## Registro em `Program.cs`

```csharp
// ─── Fase 9: Alvo Global sobre Posições Abertas (fecha tudo ao cruzar meta/stop) ──
builder.Services.Configure<Financial.Robot.Application.Configuracoes.ConfiguracaoAlvoGlobalPosicoesAbertas>(
    builder.Configuration.GetSection("AlvoGlobalPosicoesAbertas"));
builder.Services.AddHostedService<Financial.Robot.Worker.Risk.AlvoGlobalPosicoesAbertasService>();
```

## Como habilitar (depois de implementado e testado)

```json
"AlvoGlobalPosicoesAbertas": {
  "Ativo": true,
  "TerminalId": "activtraders",
  "MetaLucroGlobalPercent": 3.0,
  "StopPerdaGlobalPercent": 1.5,
  "IntervaloVerificacaoSegundos": 10
}
```

Valores acima são só exemplo (proporção 1:2, coerente com o SL:TP já usado em toda estratégia do projeto) — não fixar como default no código; `Ativo: false` por padrão.

## Critério de aceite

- Com `Ativo: false` (default), nenhuma mudança de comportamento.
- Com `Ativo: true` e posições abertas em mais de um símbolo simultaneamente (conta demo), simular lucro/prejuízo agregado cruzando o limite configurado e confirmar que **todas** as posições abertas em todos os símbolos são fechadas.
- Confirmar que, imediatamente após o fechamento, o robô continua avaliando e abrindo novas entradas normalmente — nenhum log de "trading pausado" ou similar, porque esse mecanismo não existe mais nesta versão.
- Confirmar que, se novas posições abrirem depois e cruzarem o limite de novo, o serviço dispara de novo e fecha de novo (comportamento repetível, não é "uma vez por dia").
- Nenhuma exceção não tratada derruba o serviço; falha de `FecharPosicaoAsync` vira `Warning`.
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               