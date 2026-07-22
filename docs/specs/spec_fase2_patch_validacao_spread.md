# Patch na Fase 2 já implementada — validação de SL vs. spread + unidade de distância

> A Fase 2 (`spec_fase2_config_conexao_dados.md`) foi codada e validada **antes** de duas mudanças que entraram no spec depois: o campo `unidadeDistancia` e a validação `slMinimoSobreSpread`. Este documento é só o incremento — não repete o que já foi feito (schema geral, Config Watcher, Connection Manager multi-terminal, Market Data Service), que continua válido.

## Motivação (contexto para quem for aplicar o patch)

Um backtest exploratório com BTCUSD M1 mostrou que o spread real da corretora (~18,4 USD) pode ser **maior que o ATR médio do próprio candle** (~9,25 USD). Um Stop Loss calculado com margem pequena sobre o ATR nasce menor que o custo de spread — a operação abre já praticamente estopada, independente da qualidade do sinal. Isso não é um problema só de BTCUSD: pode acontecer com qualquer ativo/timeframe/corretora onde o spread for desproporcional à volatilidade do período. O patch adiciona uma trava estrutural contra esse cenário.

## O que adicionar

### 1. Campo `UnidadeDistancia` em `SaidaConfig`

Enum (`Pips` ou `PrecoAbsoluto`) indicando como interpretar `StopLossPips`/`TakeProfitPips`/`TrailingStopPips`. Pares cripto/índices costumam fazer mais sentido em preço absoluto do que em pips.

Adicionar também, se ainda não existir, suporte a SL/TP expresso como múltiplo de ATR (`StopLossAtrMultiplo`, `TakeProfitAtrMultiplo`) — usado no exemplo `BTCUSD.config.exemplo.json` que já está na pasta `config/`.

### 2. Campo `SlMinimoSobreSpread` em `SaidaConfig`

`decimal?`, ex.: `1.5` — significa "o SL precisa ser pelo menos 1,5x o spread atual do símbolo". Se `null`, sem validação (comportamento atual, para não quebrar configs já em produção que não tenham esse campo).

### 3. Validação no `ConfigWatcherService`/`ConfigValidator` — **só quando validável estaticamente**

Importante: essa validação só é possível na carga do config quando `UnidadeDistancia` é `Pips` ou `PrecoAbsoluto` — nesses casos, dá pra converter `StopLossPips`/valor absoluto para uma distância de preço fixa e comparar contra o spread atual do símbolo (consultar via `SymbolInfoDouble`/tick no terminal indicado por `TerminalId`).

Quando o SL é definido como múltiplo de ATR (`StopLossAtrMultiplo`), o Config Watcher **não tem como validar** no momento da carga (ATR ainda não foi calculado — isso só existe em runtime, na Strategy Engine da Fase 3). Nesse caso:
- O Config Watcher aceita o config normalmente (não rejeita por falta de informação).
- Registrar como pendência para a Fase 3: a Strategy Engine deve reavaliar essa mesma regra **em tempo real, antes de cada entrada**, comparando `StopLossAtrMultiplo × ATR atual` contra o spread do momento, e abortar a entrada (sem quebrar o serviço) se a regra não for satisfeita.

Comportamento de rejeição (quando validável estaticamente e a regra falhar): mesmo padrão já implementado para outras validações — mantém o config anterior ativo, loga o motivo detalhado (valor de SL implícito, spread consultado, resultado da comparação), não derruba o serviço.

## Critério de aceite do patch

- [ ] Um config com `UnidadeDistancia: "PrecoAbsoluto"` e SL menor que `slMinimoSobreSpread × spread atual` é rejeitado na carga, com log explicando os valores comparados.
- [ ] Um config com `StopLossAtrMultiplo` (sem `StopLossPips` fixo) é aceito normalmente na carga, sem erro — a validação desse caso fica documentada como pendência explícita da Fase 3, não implementada aqui.
- [ ] `config/BTCUSD.config.exemplo.json` (já presente na pasta) carrega sem erro depois do patch.
- [ ] Configs antigos sem os campos novos (ex.: `EURUSD.config.json` atual, se não tiver `unidadeDistancia`/`slMinimoSobreSpread`) continuam carregando normalmente — os campos novos devem ser opcionais/retrocompatíveis.
