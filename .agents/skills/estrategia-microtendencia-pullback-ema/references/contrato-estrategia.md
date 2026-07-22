# Contrato tecnico: MicroTendenciaPullbackEma

## Fontes canonicas

- `src/Financial.Robot.Worker/Strategy/Estrategias/MicroTendenciaPullbackEma.cs`
- `tests/Financial.Robot.Worker.Tests/Strategy/MicroTendenciaPullbackEmaTests.cs`
- `src/Financial.Robot.Worker/Strategy/StrategyEngine.cs`
- `docs/catalogo-estrategias.md`

## Regra implementada

O lookback nativo e `emaLentaPeriodo + candlesInclinacao + 150`. Calcula internamente EMA rapida/media/lenta, RSI(14), ATR(14) e, se ligado, VWAP apenas do dia do ultimo candle.

| Parametro | Padrao |
|---|---:|
| `emaRapidaPeriodo` / `emaMediaPeriodo` / `emaLentaPeriodo` | 9 / 21 / 50 |
| `candlesInclinacao` | 3 |
| `toleranciaPullbackPreco` ou `toleranciaPullbackPontos` | 40 |
| `rompimentoMinimoPreco` ou `rompimentoMinimoPontos` | 5 |
| `spreadMaximoPreco` ou `spreadMaximoPontos` | 15 |
| `atrMinimoPreco` ou `atrMinimoPontos` | 30 |
| `candleMaxAtrMultiplo` | 1.2 |
| `rsiMaximoCompra` / `rsiMinimoVenda` | 68 / 32 |
| `usarFiltroVwap` / `exigirFechamentoDirecional` | false / true |

Compra requer: todas as EMAs em alta versus `1+candlesInclinacao` barras atras, rapida > media > lenta, Bid acima da lenta, minimo do candle na zona entre rapida e lenta com tolerancia, RSI nao acima do limite, ATR suficiente, candle nao maior que ATR*limite, VWAP valida se ligada, candle de alta se exigido e fechamento alem da maxima anterior ou EMA rapida por `rompimentoMinimo`.

Venda e o espelho, usando Ask abaixo da EMA lenta, maximo na zona, RSI nao abaixo do limite, VWAP e fechamento de baixa/rompimento inferior.

## Cenario e risco

Usar em dia direcional com pullbacks ordenados. Evitar EMAs emboladas, perda da EMA lenta, ATR baixo, candle de noticia grande, RSI ja esticado e VWAP contra a direcao. O codigo nao avalia noticia, volume ou tendencia de timeframe superior.

## Configuracao candidata

- Nome obrigatorio: `MicroTendenciaPullbackEma`.
- Validar os parametros como distancia absoluta do simbolo, nunca como pips convertidos implicitamente.
- Ligar VWAP somente se a sessao/timestamp estiverem confiaveis; ela ignora candles de dias anteriores pelo codigo.
- Preservar risco, saida e campos desconhecidos; manter `operar=false` e bloco inativo.

## Auditoria minima

Registrar series das tres EMAs, inclinacao, lado do preco, ATR, tamanho do candle/ATR, RSI, VWAP, zona de pullback, gatilho de rompimento, spread, entrada, SL/TP e resultado.
