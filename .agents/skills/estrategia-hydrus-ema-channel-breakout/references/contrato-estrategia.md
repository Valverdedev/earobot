# Contrato tecnico: HydrusEmaChannelBreakout

## Fontes canonicas

- `src/Financial.Robot.Worker/Strategy/Estrategias/HydrusEmaChannelBreakout.cs`
- `tests/Financial.Robot.Worker.Tests/Strategy/HydrusEmaChannelBreakoutTests.cs`
- `src/Financial.Robot.Worker/Strategy/StrategyEngine.cs`
- `docs/catalogo-estrategias.md`

## Regra implementada

| Parametro | Padrao | Efeito |
|---|---:|---|
| `emaPeriodo` | 156 | EMA longa calculada no fechamento. |
| `mediaCurtaPeriodo` | 5 | Periodo da media curta. |
| `mediaCurtaTipo` | `SMMA` | Aceita `SMA`, `EMA` ou, no restante dos casos, SMMA. |
| `mediaCurtaPreco` | `Median` | Usa `(high+low)/2`; outro valor usa fechamento. |
| `canalPontos` | 170 | Banda fixa acima/abaixo da EMA longa. |
| `spreadMaximoPontos` | 10 | Bloqueia `Ask-Bid` maior. |
| `distanciaMaximaAposRompimentoPontos` | 80 | Bloqueia preco perseguido alem da banda. |
| `usarConfirmacaoTimeframeMaior` | `false` | Liga filtro pela EMA de timeframe maior. |
| `emaConfirmacaoPeriodo` / `timeframeConfirmacao` | 21 / `M5` | Parametros do filtro maior. |

Exige pelo menos `emaPeriodo + 10` candles. Compra quando a media curta cruza a banda superior de baixo para cima; venda e o espelho na banda inferior. A distancia e medida por Ask na compra e Bid na venda.

Com confirmacao maior, o codigo agrupa os candles recebidos como se fossem M1; portanto manter o timeframe-base em M1 e confirmar que o historico tenha ao menos `(emaConfirmacaoPeriodo + 100) * minutosDoTimeframe` candles. Para compra pode exigir `Bid >= EMA maior`; para venda, `Bid <= EMA maior`.

Nao possui ATR, volume, reteste, fechamento de confirmacao, stop estrutural ou canal adaptativo. O canal fixo pode ficar largo ou estreito para a volatilidade corrente.

## Cenario e risco

Usar quando tendencia se inicia/retoma com canal calibrado para a volatilidade. Evitar mercado lateral, rompimento muito distante, troca de regime e recalibracao sem amostra fora da calibracao.

## Configuracao candidata

- Nome obrigatorio: `HydrusEmaChannelBreakout`.
- Validar `canalPontos` por ativo, timeframe e ATR observado, sem escrever um multiplicador ATR inexistente.
- Configurar confirmacao maior somente com base M1 e historico suficiente. Usar valores de booleano que o parser/codigo atual aceite.
- Preservar campos e limites; `operar=false` e bloco inativo.

## Auditoria minima

Registrar EMA longa atual/anterior, media curta atual/anterior, bandas, preco e distancia apos cruzamento, spread, filtro maior, timeframe-base, candles disponiveis, entrada, SL/TP e mudanca de volatilidade.
