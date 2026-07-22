# Contrato tecnico: OpeningRangeBreakout

## Fontes canonicas

- `src/Financial.Robot.Worker/Strategy/Estrategias/OpeningRangeBreakout.cs`
- `src/Financial.Robot.Worker/Strategy/StrategyEngine.cs`
- `tests/Financial.Robot.Worker.Tests/Strategy/OpeningRangeBreakoutTests.cs`
- `docs/catalogo-estrategias.md`

## Regra implementada

Exige os parametros `janelaFormacaoRange` e `janelaOperacao` no formato aceito por `ParametroParser`. O relogio usado e `tick.Timestamp`, nao `DateTime.UtcNow`. Antes de configurar, confirmar se o timestamp do broker esta em BRT, UTC ou outro fuso.

- Fora de `janelaOperacao`, aguarda.
- Monta o range com candles entre inicio e fim da janela de formacao mais recente. Suporta janela que atravessa meia-noite.
- Sem candle dentro da formacao, aguarda.
- Compra se `Ask > maxRange`; vende se `Bid < minRange`, desde que a direcao esteja habilitada.
- Nao exige fechamento fora da faixa, buffer, volume, reteste, ATR ou apenas uma entrada por direcao/sessao. Pode voltar a sinalizar enquanto o preco ficar fora do range, sujeito aos filtros posteriores do motor.
- `ObterLookbackNecessario` calcula da abertura da formacao ate o fim da janela de operacao, em candles do timeframe do primeiro indicador, e adiciona 10 candles.
- Stop no lado oposto do range e alvo proporcional ao range nao sao implementados pela classe; usar somente saida generica validada.

## Cenario e risco

Usar em abertura com expansao e aceitacao direcional, faixa inicial compacta e espaco restante para o alvo. Evitar faixa ja ampla, baixo volume, rompimento por spread, pavio isolado, retorno para dentro da faixa e horario/fuso incerto.

## Configuracao candidata

- Nome obrigatorio: `OpeningRangeBreakout`.
- Definir ambas as janelas no fuso comprovado e validar historico suficiente no timeframe-base.
- Aplicar janela externa, cooldown, maximo de operacoes e maximo de stops para reduzir repeticao, pois a classe nao o faz.
- Preservar limites existentes; deixar `operar=false` e estrategia inativa no candidato.

## Auditoria minima

Registrar fuso, inicio/fim reais, candles incluidos no range, maxima/minima, tick de disparo, distancia ja percorrida, retorno ao range, spread, eventos de abertura e repeticoes por magic number.
