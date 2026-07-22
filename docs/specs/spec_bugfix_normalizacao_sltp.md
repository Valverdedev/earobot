# Bugfix — Normalização de SL/TP ao Tick Size do Símbolo

> Spec curta de correção de bug, não uma feature nova. Escrita a partir de um erro real capturado em produção.

## Sintoma (log real)

```
[10:25:36 INF] [Execução] Abrindo SELL WINQ26 | Vol=1 SL=179867.50295178275 TP=179373.7455723259 | Terminal=broker-genial
[10:25:36 WRN] [Execução] Ordem recusada - Retcode=10016 Msg=Invalid stops
```

Retcode `10016` = `TRADE_RETCODE_INVALID_STOPS`. A corretora (Genial) recusou a ordem porque o SL/TP enviado (`179867.50295178275`) tem 11+ casas decimais — incompatível com a granularidade de preço real do símbolo WINQ26 (índice futuro, negocia em pontos inteiros / múltiplos de tick pequenos, não em frações de ponto flutuante arbitrárias).

## Causa raiz

Arquivo: `src/Financial.Robot.Worker/Strategy/StrategyEngine.cs`, método `CalcularSlTpAsync` (linhas ~237-292).

Para estratégias que usam SL/TP via múltiplo de ATR (ex.: `CruzamentoEma`, campos `StopLossAtrMultiplo`/`TakeProfitAtrMultiplo`):

```csharp
slDist = (double)saida.StopLossAtrMultiplo.Value * atr;   // linha ~254
tpDist = (double)saida.TakeProfitAtrMultiplo.Value * atr; // linha ~256
...
return lado == LadoOrdem.Compra
    ? (preco - slDist.Value, tpDist.HasValue ? preco + tpDist.Value : null)
    : (preco + slDist.Value, tpDist.HasValue ? preco - tpDist.Value : null);  // linhas ~286-288
```

`atr` vem do indicador ATR calculado sobre candles (double de precisão total). O `preco` vem do tick atual. A soma/subtração produz um double "sujo" com toda a precisão de ponto flutuante, e **esse valor é usado como SL/TP final sem nenhum arredondamento**.

O método já busca `ponto` (`SYMBOL_POINT`, via `ObterTamanhoPontoAsync`) e `stopsLevel` (`SYMBOL_TRADE_STOPS_LEVEL`, via `ObterStopsLevelAsync`), ambos implementados em `GatewayMt5.cs` (linhas ~110-123) — mas **o `ponto` é usado só para calcular a distância mínima (`minDist = stopsLevel * ponto`), nunca para normalizar o valor final de SL/TP ao grid de tick do símbolo.**

Confirmado também que não existe nenhuma normalização downstream:
- `Financial.Robot.Worker/Execution/ServicoExecucao.cs` (`AbrirPosicaoAsync`, linhas ~23-61) repassa `sl`/`tp` como recebidos, sem tocar nos valores.
- `Financial.Robot.Infrastructure/Gateways/GatewayMt5.cs` (`AbrirOrdemMercadoAsync`, linhas ~292-328) atribui `Sl = stopLoss` / `Tp = takeProfit` direto no `MqlTradeRequest` e chama `_client.OrderSend(...)` sem `Math.Round`/`NormalizeDouble`.

A checagem de distância mínima (`SYMBOL_TRADE_STOPS_LEVEL`) já existe e está correta — **o bug é exclusivamente a falta de normalização ao tick size**, não a lógica de distância mínima.

## Escopo do problema

O caminho da `PriceActionSuporteResistencia` passa pelo mesmo `CalcularSlTpAsync`, mas usa `stopLossPips`/`takeProfitPips` fixos (valores inteiros no config, ex.: 1300/2600) somados a um preço de tick — menor probabilidade de gerar um double "sujo" visível, mas tecnicamente sujeito ao mesmo risco caso o preço de tick em si tenha muitas casas decimais.

O caminho via ATR (`CruzamentoEma`) é o mais exposto, pois multiplica um valor de indicador (double de precisão total) por um multiplicador de config. **Isso afeta toda estratégia CruzamentoEma em qualquer ativo**, não só WINQ26 — hoje está ativa em XAUUSDz, EURUSDz, USTECz e WINQ26. É possível que os demais ativos (corretoras forex, mais tolerantes a casas decimais, ou índices com tick size mais fino) não estejam rejeitando a ordem visivelmente, mas o mesmo problema de precisão existe no cálculo — recomenda-se checar os logs desses ativos por `Invalid stops` também após o deploy desta correção, e antes dela, para confirmar se já ocorreu de forma silenciosa/intermitente.

## Correção proposta

Em `CalcularSlTpAsync`, após montar os valores finais de `sl`/`tp` (antes do `return`, linhas ~286-288), normalizar cada valor ao grid de tick do símbolo:

```csharp
static double NormalizarAoTick(double valor, double ponto)
{
    if (ponto <= 0) return valor; // fallback defensivo, nao deveria ocorrer
    return Math.Round(valor / ponto, MidpointRounding.AwayFromZero) * ponto;
}
```

Aplicar essa normalização ao `sl` e ao `tp` finais, cobrindo **ambos os caminhos** (ATR e pips), já que os dois passam pelo mesmo método e ambos podem em teoria produzir imprecisão de ponto flutuante. O `ponto` (`SYMBOL_POINT`) já é obtido nesse método para o cálculo de `minDist` — só precisa ser reaproveitado aqui, sem chamada adicional ao gateway.

Como proteção adicional (não estritamente necessária se a normalização acima estiver correta, mas barata e defensiva), aplicar também `Math.Round(valor, digits)` usando `SYMBOL_DIGITS` do símbolo, caso essa informação já esteja disponível via gateway — elimina qualquer resíduo de ponto flutuante que sobre após a normalização ao tick.

## Fora de escopo

- Não muda a lógica de cálculo de distância (`slDist`/`tpDist`), só o arredondamento do resultado final.
- Não muda a checagem de `SYMBOL_TRADE_STOPS_LEVEL`, que já está correta.
- Não requer mudança de schema de config — nenhum campo novo em `SymbolConfig`/`EstrategiaConfig`.

## Teste recomendado

Após a correção, reenviar manualmente (ou aguardar o próximo sinal real) uma ordem de `CruzamentoEma` no WINQ26 e confirmar no log que o `SL=`/`TP=` aparecem com poucas casas decimais (compatíveis com o tick size do símbolo) e que a ordem não é mais recusada com retcode 10016. Repetir a checagem de log (`Invalid stops`) para XAUUSDz, EURUSDz e USTECz nas execuções seguintes.
