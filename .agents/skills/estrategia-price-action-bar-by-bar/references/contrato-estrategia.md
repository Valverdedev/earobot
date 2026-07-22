# Contrato tecnico: PriceActionBarByBar

## Fontes canonicas

- `src/Financial.Robot.Worker/Strategy/Estrategias/PriceActionBarByBar.cs`
- `tests/Financial.Robot.Worker.Tests/Strategy/PriceActionBarByBarTests.cs`
- `src/Financial.Robot.Worker/Strategy/StrategyEngine.cs`
- `docs/catalogo-estrategias.md`

## Regra implementada

Nao usa indicadores. O lookback nativo e `lookbackMedioRange + candlesImpulsoMax + candlesPullbackMax + 5`; o minimo para avaliar e `lookbackMedioRange + candlesImpulsoMinimo + candlesPullbackMinimo + 2`.

| Parametro | Padrao |
|---|---:|
| `corpoMinimoFracaoRange` | 0.55 |
| `candlesImpulsoMinimo` / `candlesImpulsoMax` | 2 / 6 |
| `candlesPullbackMinimo` / `candlesPullbackMax` | 1 / 5 |
| `pullbackMaximoPercentual` | 0.7 |
| `lookbackMedioRange` | 20 |
| `climaxMultiploRange` | 2.5 |
| `bufferStopPreco` | 5.0 |

Trend bar de alta/baixa precisa ter corpo direcional ocupando ao menos a fracao configurada do range. O impulso aceita maioria forte de trend bars em uma direcao. Cada janela de impulso e bloqueada se alguma barra tiver range maior que `rangeMedio * climaxMultiploRange`.

O algoritmo usa a penultima barra como barra de sinal, apos pullback de 1..M barras que nao devolve mais que a fracao configurada da amplitude do impulso. A ultima barra precisa fechar acima da maxima da barra de sinal para comprar ou abaixo da minima para vender.

O resultado leva `StopSugerido`: minima da barra de sinal menos buffer para compra, maxima mais buffer para venda. No motor, esse stop tem prioridade sobre SL generico. O TP usa TP ATR se houver ATR disponivel; senao TP em preco se configurado; senao 2x o risco estrutural. Depois o motor aplica stops level e normalizacao de tick.

Nao filtra tendencia maior, spread, horario, volume, evento ou climax da barra de confirmacao/pullback.

## Cenario e risco

Usar em tendencia clara, impulso limpo e pullback raso sem climax. Evitar lateralidade ruidosa, liquidez baixa, primeiro movimento de noticia, sinal contra vies maior e stop estrutural anormalmente largo/curto.

## Configuracao candidata

- Nome obrigatorio: `PriceActionBarByBar`.
- Calibrar corpo/range, janela de impulso/pullback, climax e buffer somente com dados por ativo/timeframe, teste de sensibilidade e custo real.
- Validar risco real entrada-stop, tick e stops level; nao substituir stop estrutural por SL arbitrario.
- Usar gates externos para spread, horario, direcao e risco. Preservar campos; manter `operar=false` e bloco inativo.

## Auditoria minima

Para cada ordem, arquivar candles do range medio, impulso, pullback, barra de sinal e confirmacao; valores dos parametros, range medio, climax, stop sugerido, entrada, TP calculado, spread, direcao de contexto e resultado. Sinal com confirmacao incompleta ou contra direcao permitida e nao aderente.
