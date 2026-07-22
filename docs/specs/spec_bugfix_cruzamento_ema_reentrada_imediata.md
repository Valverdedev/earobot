# Bugfix — CruzamentoEma reabre posição imediatamente após fechar a anterior

> Investigado a pedido do usuário: "tenho percebido no cruzamento de media, que uma ordem é aberta logo apos o fechamento da outra, as vezes no mesmo candle, as vezes no candle seguinte, principalmente em XAU."

## Sintoma observado (confirmado com dados reais)

`get_deals XAUUSDz` de 2026-07-14, 00:15-00:37 UTC (22 minutos): 11 trades fechados pela `CruzamentoEma` (magic 502), reabrindo em segundos após o fechamento da posição anterior:

| Fecha (hora) | Profit | Reabre (hora) | Delta |
|---|---|---|---|
| 00:19:50 | +3.52 | 00:20:00 | 10s |
| 00:21:21 | -1.65 | 00:22:00 | 39s |
| 00:24:57 | -1.57 | 00:25:09 | 12s |
| 00:27:52 | -1.47 | 00:28:03 | 11s |
| 00:28:42 | -3.73 | 00:30:00 | 78s |
| 00:30:29 | -1.55 | 00:31:00 | 31s |
| 00:32:59 | +4.79 | 00:33:00 | **1s** |
| 00:33:10 | -1.30 | 00:34:01 | 51s |
| 00:34:30 | +3.83 | 00:35:00 | 30s |
| 00:36:21 | -1.75 | 00:37:00 | 39s |

Resultado líquido do período: praticamente neutro (~-0.88 bruto, ~-2.09 líquido após comissão), mas o padrão de reentrada instantânea é o problema estrutural, independente do resultado financeiro pontual — é um comportamento não intencional de uma estratégia de "cruzamento", que deveria disparar uma vez por cruzamento, não continuamente enquanto a condição persiste.

## Causa raiz

**1. `CruzamentoEma.Avaliar` é *level-triggered*, não *edge-triggered*** (`src/Financial.Robot.Worker/Strategy/Estrategias/CruzamentoEma.cs`, linhas 35-47):

```csharp
if (config.Comprar && ema9Val > ema21Val && rsiOkCompra)
{
    ...
    return ResultadoDecisao.Comprar(...);
}
```

A condição é avaliada isoladamente a cada novo candle fechado, sem nenhuma memória do estado anterior (não guarda se `ema9 <= ema21` no candle anterior). Isso significa que a estratégia sinaliza "Comprar" em **todo** candle em que `ema9 > ema21` continuar verdadeiro — não apenas no candle em que o cruzamento efetivamente ocorreu. Para uma estratégia de "cruzamento de médias", o comportamento correto seria disparar apenas na transição (edge), não enquanto a condição persiste (level).

**2. `StrategyEngine` não tem cooldown pós-fechamento** (`src/Financial.Robot.Worker/Strategy/StrategyEngine.cs`, `AvaliarEntradaAsync`, linhas 184-187):

```csharp
var maxOperacoes = _estrategiaConfig.GestaoDeRisco?.MaxOperacoesSimultaneas ?? 1;
var posicoesAbertas = await _gateway.ObterTicketsPosicoesAbertasAsync(brokerSymbol, _estrategiaConfig.MagicNumber, ct);
if (!_riskGuard.ValidarMaxOperacoes(maxOperacoes, posicoesAbertas.Count, brokerSymbol, $"Magic={_estrategiaConfig.MagicNumber}")) return;
```

A única trava contra reentrada é a contagem de posições **abertas agora**. Assim que uma posição fecha (SL/TP atingido, verificado continuamente pelo MT5, não pelo loop do robô), a contagem volta a 0 no instante seguinte. Como a avaliação roda a cada novo candle fechado (`LoopAsync`, linha 107-118) e no timeframe M1 isso é a cada ~60s, o próximo candle fechado (às vezes literalmente o candle seguinte, segundos depois) já passa livre pela trava de `maxOperacoesSimultaneas` e, combinado com o problema #1, reabre imediatamente na mesma direção se a condição de EMA ainda for válida — o que é o caso comum, já que um stop-out não necessariamente inverte a relação EMA9/EMA21.

**Por que principalmente no XAUUSDz**: é o único ativo do projeto que manteve a `CruzamentoEma` em timeframe M1 (EURUSDz e USTECz já foram migrados para M5 anteriormente nesta mesma sessão, por um problema relacionado mas distinto — ATR M1 pequeno demais frente ao custo fixo de comissão, causando churn por outro motivo). Em M1, o ciclo de avaliação é muito mais rápido, dando à combinação dos problemas #1 e #2 muito mais oportunidades de disparar por hora do que nos ativos já em M5.

## Correção proposta

### 1. Edge-detection no `CruzamentoEma` (correção principal)

Guardar o estado do cruzamento no candle anterior (por instância de `StrategyEngine`/magic) e só disparar quando a relação **muda** de `ema9 <= ema21` para `ema9 > ema21` (compra) ou vice-versa (venda):

```csharp
// Pseudocódigo — precisa de estado por estratégia/símbolo, hoje IEstrategiaEntrada.Avaliar é stateless
var cruzouParaCima = ema9Anterior <= ema21Anterior && ema9Val > ema21Val;
var cruzouParaBaixo = ema9Anterior >= ema21Anterior && ema9Val < ema21Val;

if (config.Comprar && cruzouParaCima && rsiOkCompra) { ... }
if (config.Vender && cruzouParaBaixo && rsiOkVenda) { ... }
```

Isso exige adicionar estado à estratégia (hoje `IEstrategiaEntrada.Avaliar` é stateless, recebe só a lista de candles/indicadores a cada chamada) — a forma mais simples é calcular EMA9/EMA21 do candle N-1 a partir da própria lista `candles` já passada (reaproveitando o cálculo do indicador sobre uma janela deslocada em 1), sem precisar de campo de instância mutável.

### 2. Cooldown pós-fechamento (defesa em profundidade, recomendado independente da correção 1)

Adicionar um novo campo opcional em `SaidaConfig` (ex.: `cooldownAposFechamentoSegundos`), e em `StrategyEngine.AvaliarEntradaAsync`, antes da checagem de `ValidarMaxOperacoes`, verificar se o último fechamento de posição para aquele magic number foi há menos tempo que o cooldown configurado (via `ObterDealsAsync`/`ObterLucroPrejuizoDiaAsync` já existentes, ou um novo método de gateway que retorna o timestamp do último fechamento) — se sim, aguardar (`ResultadoDecisao.Aguardar`) em vez de abrir nova posição.

Isso cobre não só o caso específico do `CruzamentoEma` mas qualquer estratégia futura com o mesmo padrão de risco, e é útil mesmo depois da correção 1, como rede de segurança genérica contra reentrada agressiva após stop-out.

## Mitigação imediata (via config, sem mudança de código)

Enquanto a correção de código não é implementada, o `stopLossAtrMultiplo`/`takeProfitAtrMultiplo` da `CruzamentoEma` no XAUUSDz podem ser aumentados (reduzindo a frequência de stop-outs rápidos) ou a estratégia pode ser temporariamente desativada (`ativa: false`) neste ativo especificamente, mesmo padrão de decisão já usado para EURUSDz/USTECz enquanto seus ajustes de ATR/timeframe eram testados. Ver notas de config do XAUUSDz para a decisão tomada.

## Teste recomendado

1. Aplicar correção 1, confirmar via `get_deals` que a `CruzamentoEma` do XAUUSDz não abre mais de uma posição por cruzamento real de EMA (não por candle onde a condição persiste).
2. Aplicar correção 2, confirmar que nenhuma nova posição abre dentro da janela de cooldown configurada após um fechamento, independente do motivo do fechamento (SL, TP, ou fechamento manual/global).
3. Validar que os demais ativos com `CruzamentoEma` (EURUSDz M5, USTECz M1, WINQ26 M1) não tiveram sua frequência normal de entrada reduzida além do esperado pela correção (edge-detection deve, na prática, ser transparente/positivo mesmo nesses ativos — hoje eles também sofrem do mesmo bug #1 em menor grau, só não é tão visível por já operarem em timeframes mais espaçados ou por não terem tido stop-outs tão rápidos).
