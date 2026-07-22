# Fase 8 — Gestão Dinâmica de Posições Abertas (Breakeven + Trailing Stop)

## Contexto e motivação

Hoje a gestão de uma posição aberta termina no momento em que ela é enviada: SL e TP são fixados na abertura (`AbrirPosicaoAsync`) e nada mais acontece até um dos dois ser atingido. O schema de config já reserva um campo `trailingStopPips` em `SaidaConfig` desde a Fase 5, mas ele **nunca foi lido em lugar nenhum do código** — é um campo morto. Não existe breakeven, não existe qualquer ajuste dinâmico de SL depois da abertura.

Decisão do usuário (12/07/2026): perfil de gestão "nem muito lucro, nem muito prejuízo" — priorizar **breakeven automático** (travar a posição em zero-a-zero assim que ela andar a favor o suficiente) + **trailing stop** (deixar o SL perseguir o preço depois disso, sem definir um teto rígido de lucro). Outras ideias levantadas (saída por tempo, saída parcial em dois alvos, saída por janela de calendário) ficam para uma fase futura — não implementar nesta fase.

## Objetivo desta fase

Implementar um serviço independente que, periodicamente, percorre todas as posições abertas de todas as estratégias ativas e:

1. **Breakeven**: quando o lucro da posição (em pontos) atingir `BreakevenGatilhoAtrMultiplo × ATR`, move o SL para `precoAbertura + BreakevenBufferPips` (compra) ou `precoAbertura - BreakevenBufferPips` (venda) — nunca antes disso, e nunca afrouxa um SL que já esteja melhor.
2. **Trailing stop**: se `TrailingStopAtrMultiplo` (ou o já existente `TrailingStopPips`, fixo) estiver configurado, o SL persegue o preço atual a essa distância, sempre na direção favorável, nunca podendo piorar o SL existente.

Isso deve funcionar por estratégia (isolado por `MagicNumber`, mesma filosofia de toda a Fase 5), sobre qualquer símbolo/terminal já configurado, sem exigir mudança nas estratégias de entrada existentes.

## 1. `SaidaConfig` — novos campos (aditivos, retrocompatíveis)

Arquivo: `src/Financial.Robot.Domain/ValueObjects/SaidaConfig.cs`

```csharp
namespace Financial.Robot.Domain.ValueObjects;

public record SaidaConfig(
    double? EncerrarAtivaAcimaDe,
    double? EncerrarAtivaAbaixoDe,
    decimal? StopLossPips,
    decimal? TakeProfitPips,
    decimal? TrailingStopPips,
    UnidadeDistancia? UnidadeDistancia,
    decimal? StopLossAtrMultiplo,
    decimal? TakeProfitAtrMultiplo,
    decimal? SlMinimoSobreSpread,
    bool ApplyToOpenPositions,
    decimal? BreakevenGatilhoAtrMultiplo = null,
    decimal? BreakevenBufferPips = null,
    decimal? TrailingStopAtrMultiplo = null
);
```

**IMPORTANTE — mesma classe de bug já vista na Fase 6/7**: os três campos novos são `decimal?` (nullable) com valor default `null` no próprio construtor. Isso é obrigatório para que configs JSON existentes (que não têm essas chaves) continuem desserializando sem erro. Nunca torne esses campos não-nulos.

Regra de precedência quando ambos `TrailingStopPips` (fixo) e `TrailingStopAtrMultiplo` estiverem preenchidos no mesmo `SaidaConfig`: `TrailingStopPips` (distância fixa) tem prioridade — é mais previsível para quem está calibrando manualmente. Se só `TrailingStopAtrMultiplo` estiver preenchido, a distância é recalculada a cada ciclo com o ATR atual.

## 2. Novo método de leitura no Gateway — detalhes completos da posição

O gateway hoje só expõe `ObterTicketsPosicoesAbertasAsync` (retorna só o ticket). Para calcular breakeven/trailing precisamos de preço de abertura, preço atual, SL atual, tipo (compra/venda) e volume.

### `src/Financial.Robot.Domain/Interfaces/IGatewayMt5.cs` — adicionar

```csharp
/// <summary>Obtém detalhes completos das posições abertas para um símbolo (e opcionalmente magic number).</summary>
Task<IReadOnlyList<DetalhesPosicaoMt5>> ObterDetalhesPosicoesAbertasAsync(
    string simbolo, long? magicNumber = null, CancellationToken ct = default);
```

E o novo record (no mesmo arquivo, junto aos outros records de retorno do gateway):

```csharp
/// <summary>Detalhes completos de uma posição aberta, usados para gestão dinâmica (breakeven/trailing).</summary>
public sealed record DetalhesPosicaoMt5(
    ulong Ticket,
    string Simbolo,
    long MagicNumber,
    bool Compra,              // true = compra, false = venda
    double PrecoAbertura,
    double PrecoAtual,
    double StopLossAtual,
    double TakeProfitAtual,
    double Volume);
```

### `src/Financial.Robot.Infrastructure/Gateways/GatewayMt5.cs` — implementação

Seguir exatamente o mesmo padrão de iteração já usado em `ObterTicketsPosicoesAbertasAsync` e `ObterLucroAbertoAsync` (ambos no mesmo arquivo, por volta da linha 164-240):

```csharp
public Task<IReadOnlyList<DetalhesPosicaoMt5>> ObterDetalhesPosicoesAbertasAsync(
    string simbolo, long? magicNumber = null, CancellationToken ct = default)
{
    ValidarConexao();
    var resultado = new List<DetalhesPosicaoMt5>();
    var total = _client.PositionsTotal();

    for (int i = 0; i < total; i++)
    {
        var ticket = _client.PositionGetTicket(i);
        var sym = _client.PositionGetString(ENUM_POSITION_PROPERTY_STRING.POSITION_SYMBOL);
        if (sym != simbolo) continue;

        var magic = _client.PositionGetInteger(ENUM_POSITION_PROPERTY_INTEGER.POSITION_MAGIC);
        if (magicNumber.HasValue && magic != magicNumber.Value) continue;

        var tipo = _client.PositionGetInteger(ENUM_POSITION_PROPERTY_INTEGER.POSITION_TYPE);
        var compra = tipo == (long)ENUM_POSITION_TYPE.POSITION_TYPE_BUY;

        resultado.Add(new DetalhesPosicaoMt5(
            Ticket: ticket,
            Simbolo: sym,
            MagicNumber: magic,
            Compra: compra,
            PrecoAbertura: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_PRICE_OPEN),
            PrecoAtual: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_PRICE_CURRENT),
            StopLossAtual: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_SL),
            TakeProfitAtual: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_TP),
            Volume: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_VOLUME)));
    }

    return Task.FromResult<IReadOnlyList<DetalhesPosicaoMt5>>(resultado);
}
```

Nota: confirme o nome exato do enum `ENUM_POSITION_TYPE.POSITION_TYPE_BUY` na versão do MtApi5 usada no projeto (deve já existir, é padrão da lib — só não foi usado ainda em nenhum arquivo).

### `src/Financial.Robot.Infrastructure/Gateways/GatewayMt5SomenteLeitura.cs` — passthrough

É leitura pura, então repassa direto ao inner gateway como os demais métodos de leitura (linha ~54-61):

```csharp
public Task<IReadOnlyList<DetalhesPosicaoMt5>> ObterDetalhesPosicoesAbertasAsync(
    string simbolo, long? magicNumber = null, CancellationToken ct = default)
    => _inner.ObterDetalhesPosicoesAbertasAsync(simbolo, magicNumber, ct);
```

## 3. Novo serviço: `GerenciadorPosicoesAbertasService`

Novo arquivo: `src/Financial.Robot.Worker/Execution/GerenciadorPosicoesAbertasService.cs`

Roda como `BackgroundService`, independente do `StrategyEngineFactory` — só precisa do `ConfigWatcherService` (mesma fonte de configs ativas) e do `ConnectionManagerService` (mesmo padrão usado em toda a Fase 5). Não depende de nenhum estado interno do `StrategyEngineFactory`.

```csharp
using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Infrastructure.Mt5;
using Financial.Robot.Worker.Config;
using Financial.Robot.Worker.Execution;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Execution;

/// <summary>
/// Fase 8 — percorre periodicamente as posições abertas de todas as estratégias ativas
/// e aplica breakeven + trailing stop conforme configurado em cada SaidaConfig.
/// Nunca abre/fecha posições — só ajusta SL via ModificarPosicaoAsync.
/// </summary>
public sealed class GerenciadorPosicoesAbertasService : BackgroundService
{
    private readonly ConfigWatcherService _configWatcher;
    private readonly ConnectionManagerService _connectionManager;
    private readonly IServicoExecucao _execucao;
    private readonly ILogger<GerenciadorPosicoesAbertasService> _logger;

    private static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromSeconds(10);

    public GerenciadorPosicoesAbertasService(
        ConfigWatcherService configWatcher,
        ConnectionManagerService connectionManager,
        IServicoExecucao execucao,
        ILogger<GerenciadorPosicoesAbertasService> logger)
    {
        _configWatcher = configWatcher;
        _connectionManager = connectionManager;
        _execucao = execucao;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloVerificacao);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessarTodasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GestaoDinamica] Erro no ciclo de verificação.");
            }
        }
    }

    private async Task ProcessarTodasAsync(CancellationToken ct)
    {
        foreach (var config in _configWatcher.GetActiveConfigs())
        {
            if (!config.Operar) continue;

            var estrategias = config.Estrategias is { Count: > 0 }
                ? config.Estrategias
                : new List<EstrategiaConfig> { CriarEstrategiaFallback(config) };

            foreach (var est in estrategias.Where(e => e.Ativa))
            {
                var saida = est.Saida ?? config.Saida;
                var temBreakeven = saida.BreakevenGatilhoAtrMultiplo is > 0;
                var temTrailing = saida.TrailingStopPips is > 0 || saida.TrailingStopAtrMultiplo is > 0;
                if (!temBreakeven && !temTrailing) continue; // nada a fazer para esta estratégia

                await ProcessarEstrategiaAsync(config, est, saida, ct);
            }
        }
    }

    private async Task ProcessarEstrategiaAsync(
        SymbolConfig config, EstrategiaConfig est, SaidaConfig saida, CancellationToken ct)
    {
        var gateway = _connectionManager.GetClient(config.TerminalId);
        var posicoes = await gateway.ObterDetalhesPosicoesAbertasAsync(config.Symbol, est.MagicNumber, ct);
        if (posicoes.Count == 0) return;

        double? atr = null;
        if (saida.BreakevenGatilhoAtrMultiplo is > 0 || saida.TrailingStopAtrMultiplo is > 0)
        {
            atr = await CalcularAtrAsync(gateway, config.Symbol, ct);
            if (atr is null or <= 0)
            {
                _logger.LogWarning("[GestaoDinamica] {Simbolo}/{Estrategia}: ATR indisponível, pulando ciclo.", config.Symbol, est.Nome);
                return;
            }
        }

        foreach (var pos in posicoes)
        {
            var novoSl = CalcularNovoStopLoss(pos, saida, atr);
            if (novoSl is null) continue;

            var resultado = await _execucao.ModificarPosicaoAsync(
                config.TerminalId, pos.Ticket, novoSl.Value, pos.TakeProfitAtual, ct);

            if (resultado.Sucesso)
            {
                _logger.LogInformation(
                    "[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket} SL {Antigo:F5} -> {Novo:F5}",
                    config.Symbol, est.Nome, pos.Ticket, pos.StopLossAtual, novoSl.Value);
            }
            else
            {
                // Falha esperada e não-crítica se o preço estiver dentro do STOPS_LEVEL do símbolo —
                // tenta de novo no próximo ciclo, não é motivo para exceção.
                _logger.LogWarning(
                    "[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket} falha ao mover SL para {Novo:F5}: {Erro}",
                    config.Symbol, est.Nome, pos.Ticket, novoSl.Value, resultado.MensagemErro);
            }
        }
    }

    /// <summary>
    /// Calcula o novo SL (breakeven e/ou trailing), ou null se nenhuma melhora se aplica.
    /// Nunca retorna um SL pior do que o atual.
    /// </summary>
    private static double? CalcularNovoStopLoss(DetalhesPosicaoMt5 pos, SaidaConfig saida, double? atr)
    {
        double? candidato = null;

        // --- Breakeven ---
        if (saida.BreakevenGatilhoAtrMultiplo is > 0 && atr is > 0)
        {
            var gatilho = (double)saida.BreakevenGatilhoAtrMultiplo.Value * atr.Value;
            var buffer = (double?)saida.BreakevenBufferPips ?? 0;
            var lucroAtual = pos.Compra ? pos.PrecoAtual - pos.PrecoAbertura : pos.PrecoAbertura - pos.PrecoAtual;

            if (lucroAtual >= gatilho)
            {
                var slBreakeven = pos.Compra ? pos.PrecoAbertura + buffer : pos.PrecoAbertura - buffer;
                candidato = MelhorSl(candidato, slBreakeven, pos.Compra);
            }
        }

        // --- Trailing stop ---
        double? distanciaTrailing = saida.TrailingStopPips is > 0
            ? (double)saida.TrailingStopPips.Value
            : (saida.TrailingStopAtrMultiplo is > 0 && atr is > 0 ? (double)saida.TrailingStopAtrMultiplo.Value * atr.Value : null);

        if (distanciaTrailing is > 0)
        {
            var slTrailing = pos.Compra ? pos.PrecoAtual - distanciaTrailing.Value : pos.PrecoAtual + distanciaTrailing.Value;
            candidato = MelhorSl(candidato, slTrailing, pos.Compra);
        }

        if (candidato is null) return null;

        // Nunca aplica um SL pior que o atual (SL=0 conta como "sem SL definido", qualquer candidato é melhora).
        var atualValido = pos.StopLossAtual > 0;
        if (atualValido)
        {
            var melhora = pos.Compra ? candidato.Value > pos.StopLossAtual : candidato.Value < pos.StopLossAtual;
            if (!melhora) return null;
        }

        return candidato;
    }

    private static double? MelhorSl(double? atual, double novoCandidato, bool compra)
    {
        if (atual is null) return novoCandidato;
        return compra ? Math.Max(atual.Value, novoCandidato) : Math.Min(atual.Value, novoCandidato);
    }

    /// <summary>ATR14 sobre o timeframe M1, calculado sob demanda (mesma fórmula usada no resto do projeto).</summary>
    private static async Task<double?> CalcularAtrAsync(IGatewayMt5 gateway, string simbolo, CancellationToken ct)
    {
        var candles = await gateway.ObterCandlesAsync(simbolo, "M1", 15, ct);
        if (candles.Count < 15) return null;

        double somaTr = 0;
        for (int i = 1; i < candles.Count; i++)
        {
            var atual = candles[i];
            var anterior = candles[i - 1];
            var tr = Math.Max(atual.High - atual.Low,
                     Math.Max(Math.Abs(atual.High - anterior.Close), Math.Abs(atual.Low - anterior.Close)));
            somaTr += tr;
        }

        return somaTr / (candles.Count - 1);
    }

    private static EstrategiaConfig CriarEstrategiaFallback(SymbolConfig config) => new(
        Id: "cruzamento-ema", Nome: "CruzamentoEma", MagicNumber: 1, Ativa: true,
        Comprar: config.Comprar, Vender: config.Vender, Entrada: config.Entrada,
        Saida: config.Saida, GestaoDeRisco: config.GestaoDeRisco,
        JanelaHorarioPermitido: config.GestaoDeRisco?.JanelaHorarioPermitido,
        Indicadores: config.Indicadores, Parametros: new Dictionary<string, object>());
}
```

Pontos de atenção para quem implementar:

- **Nunca fechar nem abrir posição** — este serviço só chama `ModificarPosicaoAsync`. Fechamento continua sendo responsabilidade exclusiva do próprio MT5 quando o preço toca SL/TP.
- **`GatewayMt5SomenteLeitura` já protege o terminal `broker-genial`** — como esse serviço só faz leitura + `ModificarPosicaoAsync` (que o decorator já intercepta e simula), não é necessário nenhum código extra para respeitar o modo somente-leitura.
- **Falha de `ModificarPosicaoAsync` por `STOPS_LEVEL`** é esperada e não crítica — o preço pode estar perto demais para o SL ser aceito pela corretora; o log fica em `Warning`, não `Error`, e o próximo ciclo tenta de novo (o preço se distancia ou o STOPS_LEVEL some).
- **ATR recalculado a cada ciclo por estratégia que precisa dele** (não reaproveita o ATR já calculado internamente pela `StrategyEngine` de entrada, que é um objeto separado) — simples e desacoplado, ao custo de uma chamada `ObterCandlesAsync` extra a cada 10s por estratégia com breakeven/trailing ativo. Aceitável dado o volume de símbolos atual (7 ativos, no máximo ~2 estratégias cada).

## 4. Registro em `Program.cs`

Adicionar junto ao bloco "Fase 3: Strategy Engine" (depois da linha `builder.Services.AddHostedService<StrategyEngineFactory>();`):

```csharp
// ─── Fase 8: Gestão Dinâmica de Posições (Breakeven + Trailing) ─────────────
builder.Services.AddHostedService<Financial.Robot.Worker.Execution.GerenciadorPosicoesAbertasService>();
```

## 5. Como habilitar nos configs existentes

Nada é ativado por padrão — todo `SaidaConfig` sem os três campos novos continua se comportando exatamente como hoje (só SL/TP fixo). Para habilitar por estratégia, adicionar ao bloco `saida` dela:

```json
"saida": {
  "stopLossPips": 100,
  "takeProfitPips": 200,
  "breakevenGatilhoAtrMultiplo": 1.0,
  "breakevenBufferPips": 5,
  "trailingStopAtrMultiplo": 1.5
}
```

Sugestão de calibração inicial (perfil "nem muito lucro, nem muito prejuízo" pedido pelo usuário): `breakevenGatilhoAtrMultiplo=1.0` (trava em zero-a-zero assim que o trade andar 1x ATR a favor) + `trailingStopAtrMultiplo=1.5` (SL persegue a 1.5x ATR de distância depois disso). Combinado com o SL inicial de ~1.5x ATR já usado em todos os configs atuais, isso dá margem para respirar sem devolver todo o lucro se o mercado reverter.

Depois que este serviço estiver implementado e testado, é uma tarefa separada (não desta fase) aplicar esses dois campos nos sete configs já existentes (BTCUSDz, ETHBTCz, XAUUSDz, HK50z, EURUSDz e demais).

## 6. Critério de aceite

- Compila sem erros; `SaidaConfig` antigo (sem os 3 campos novos) continua desserializando qualquer config JSON já existente sem exceção.
- Com `breakevenGatilhoAtrMultiplo` e/ou `trailingStopAtrMultiplo` setados em pelo menos uma estratégia de teste (demo), abrir uma posição manualmente ou via robô e confirmar nos logs (`[GestaoDinamica] ... SL ... -> ...`) que o SL se move na direção certa conforme o preço anda a favor, e nunca se move contra.
- Confirmar via `get_all_positions`/histórico do MT5 que o SL reportado pela corretora bate com o log do robô.
- Rodar por pelo menos um ciclo completo de mercado aberto sem gerar exceções não tratadas no `GerenciadorPosicoesAbertasService` (erros de `ModificarPosicaoAsync` devem virar `Warning`, nunca derrubar o serviço).
