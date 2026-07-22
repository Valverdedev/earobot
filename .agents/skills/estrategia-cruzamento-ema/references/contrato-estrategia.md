# Contrato tecnico: CruzamentoEma

## Fontes canonicas

- `src/Financial.Robot.Worker/Strategy/Estrategias/CruzamentoEma.cs`
- `src/Financial.Robot.Worker/Strategy/StrategyEngine.cs`
- `docs/catalogo-estrategias.md`

Reabrir a fonte C# quando houver alteracao no repositorio. Este documento descreve o codigo revisado em 2026-07-16.

## Regra implementada

- Exige pelo menos duas configuracoes de indicador `EMA`.
- Ordena as EMAs por `parametros.periodo` somente quando o valor chega como `int`; por isso declarar a EMA rapida antes da lenta no JSON continua sendo necessario para evitar ordem incorreta.
- Calcula EMA atual pelos indicadores ja calculados e a EMA anterior recalculando os candles sem o ultimo.
- Compra somente quando a rapida era menor ou igual a lenta e fica maior; venda e o espelho. E um gatilho de cruzamento real, nao de permanencia acima/abaixo.
- RSI e opcional. Compra requer 40 a 70; venda requer 30 a 60.
- `ForcaCesta` e opcional. Os limiares padrao sao 20 para compra e -20 para venda. So bloqueia se `forcaCestaBloqueiaEntrada` puder ser lido como string `true`; parametros que chegam como `JsonElement` podem cair nos padroes.
- Nao implementa filtro de inclinacao, ADX, distancia entre EMAs, ATR, spread, horario ou tendencia maior. Essas protecoes pertencem ao config/motor e a analise externa.

## Cenario e risco

Usar em saida de consolidacao ou retomada apos pullback. Evitar medias entrelacadas, lateralidade estreita, noticia com reversao imediata, cruzamento atrasado e custo alto relativo ao deslocamento medio.

## Configuracao candidata

- Nome obrigatorio: `CruzamentoEma`.
- Incluir duas EMAs com periodo inteiro e rapida declarada antes da lenta; incluir RSI e ForcaCesta apenas se suas fontes estiverem disponiveis e validadas.
- Nao criar parametros de RSI na estrategia: os limites estao fixos no codigo.
- `operar=false`, `ativa=false` por padrao no candidato; preservar magic number, limites, saida e campos desconhecidos.
- Validar unidade de SL/TP, tick, stops level, lote, janela, cooldown e maximo de operacoes no motor antes de qualquer promocao.

## Auditoria minima

Para cada ordem, registrar EMA rapida/lenta atual e anterior, RSI, ForcaCesta, candle de cruzamento, spread, direcao permitida, horario, magic number, stop/TP e motivo do motor. Classificar como aderente, parcial ou fora da regra.
