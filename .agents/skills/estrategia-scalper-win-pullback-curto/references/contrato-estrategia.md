# Contrato tecnico: ScalperWinPullbackCurto

## Fontes canonicas

- `src/Financial.Robot.Worker/Strategy/Estrategias/ScalperWinPullbackCurto.cs`
- `tests/Financial.Robot.Worker.Tests/Strategy/ScalperWinPullbackCurtoTests.cs`
- `src/Financial.Robot.Worker/Strategy/StrategyEngine.cs`
- `docs/catalogo-estrategias.md`

## Regra implementada

Exige pelo menos 105 candles, `niveis` manuais e os seguintes indicadores no bloco da estrategia: EMA 9 M1, RSI M1, VWAP M1, EMA 9 M5 e EMA 21 M5. Se qualquer um estiver ausente, aguarda.

| Parametro | Padrao |
|---|---:|
| `toleranciaNivelPreco` ou `toleranciaNivelPontos` | 35 |
| `spreadMaximoPreco` ou `spreadMaximoPontos` | 10 |
| `distanciaMinimaVwapPreco` ou `distanciaMinimaVwapPontos` | 40 |
| `rsiMaximoVenda` | 55 |
| `rsiMinimoCompra` | 40 |

Antes do sinal, bloqueia se `Ask-Bid` for maior que o limite ou se a distancia absoluta de `Bid` a VWAP M1 for menor que o minimo.

Venda: ultimo candle M1 testa resistencia, e baixista com sombra superior de pelo menos 2x o corpo, fecha abaixo da EMA9 M1, RSI M1 nao supera o maximo e `Bid` esta abaixo de EMA9 e EMA21 M5.

Compra e o espelho: teste de suporte, candle de alta com sombra inferior de ao menos 2x o corpo, fecha acima da EMA9 M1, RSI acima do minimo e `Bid` acima das duas EMAs M5.

A classe nao bloqueia simbolo diferente de WIN, nao mede inclinacao M5, ATR, volume, horario, evento ou stop estrutural. A saida e do motor.

## Cenario e risco

Usar em tendencia M5 clara, pullback curto em nivel atualizado e retomada M1, longe da VWAP. Evitar chop, VWAP proxima, medias M5 sem separacao, spread alto, noticia e nivel vencido.

## Configuracao candidata

- Nome obrigatorio: `ScalperWinPullbackCurto`.
- Declarar todos os cinco indicadores com nome, timeframe e periodo exatos. Validar que o primeiro indicador define o timeframe-base esperado pelo motor.
- Usar somente parametros listados; niveis devem ser tipados, datados e compatveis com a unidade do WIN.
- Preservar magic/risco/saida/campos desconhecidos; usar `operar=false` e bloco inativo.

## Auditoria minima

Para cada ordem, conservar M1 e M5, nivel, corpo/sombra, EMA9 M1, RSI, VWAP e distancia, EMAs M5, spread, horario, entrada, SL/TP e resultado. Qualquer ausencia de uma condicao torna a entrada nao aderente.
