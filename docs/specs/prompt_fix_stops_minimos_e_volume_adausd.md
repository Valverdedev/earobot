# Prompt para corrigir bug — stops mínimos da corretora não validados + volume inválido no ADAUSD

## Contexto

Depois do patch do bug de `periodo` (EMA9/EMA21 agora calculam corretamente e o cruzamento passou a disparar ordens), rodei o sistema com BTCUSD, BTCLTC, BTCETH e ADAUSD em modo normal e cruzei o log com o estado real da conta no MetaTrader (`get_all_positions`, `get_deals`). BTCLTC, BTCETH e BTCUSD abriram posição com sucesso e bateram exatamente com o broker (ticket, preço, volume). Dois problemas novos apareceram nesse teste.

## Bug 1 — Distância mínima de stop da corretora não é validada antes de enviar a ordem

No log, o BTCUSD levou duas rejeições antes de conseguir abrir:

```
17:11:41 [INF] [Execução] Abrindo BUY BTCUSD | Vol=1 SL=64254.56036026623 TP=64327.59982192642 | Terminal=broker-a
17:11:41 [WRN] [Execução] ❌ Ordem recusada — Retcode=10016 Msg=Invalid stops

17:12:45 [INF] [Execução] Abrindo BUY BTCUSD | Vol=1 SL=64259.92347739007 TP=64330.75869178881 | Terminal=broker-a
17:12:45 [WRN] [Execução] ❌ Ordem recusada — Retcode=10016 Msg=Invalid stops

17:13:40 [INF] [Execução] Abrindo BUY BTCUSD | Vol=1 SL=64282.3726218622 TP=64368.20064237533 | Terminal=broker-a
17:13:41 [INF] [Execução] ✅ Ordem aberta — Ticket=4660395536 Preço=64323.026 Retcode=10009
```

Padrão observado: nas duas tentativas rejeitadas, o ATR calculado no momento era menor (7,6884 e 7,4563), gerando SL mais próximo do preço de entrada. Na tentativa que funcionou, o ATR estava maior (9,0345), SL mais distante. Isso é o comportamento clássico de `Retcode=10016 (Invalid stops)` no MT5: a corretora tem uma distância mínima obrigatória entre o preço de entrada e o SL/TP (`SYMBOL_TRADE_STOPS_LEVEL`, em points), e o sistema hoje só valida SL contra o spread (`slMinimoSobreSpread`), não contra esse mínimo da corretora.

### Correção

No `ServicoExecucao` (ou onde a ordem é montada antes de enviar), antes de chamar `AbrirPosicaoAsync`:

1. Consultar `SYMBOL_TRADE_STOPS_LEVEL` (e `SYMBOL_TRADE_FREEZE_LEVEL`, se aplicável) do símbolo via `SymbolInfoInteger`/`SymbolInfoDouble` do gateway MT5.
2. Calcular a distância mínima em preço (stops_level × point do símbolo).
3. Se a distância SL/TP calculada pela estratégia (via ATR ou preço absoluto) for menor que esse mínimo, **ajustar para o mínimo permitido** (não simplesmente rejeitar a entrada — isso desperdiçaria um sinal válido só porque o ATR do momento estava baixo) ou, alternativamente, abortar a entrada e logar como `Warning` explicando a distância exigida vs. calculada — a decisão entre "ajustar" ou "abortar" fica a critério de quem implementar, mas o comportamento atual (deixar a corretora rejeitar e não fazer nada a respeito) não deve continuar.
4. Logar sempre que esse ajuste/rejeição acontecer, incluindo o valor de `SYMBOL_TRADE_STOPS_LEVEL` consultado, para facilitar diagnóstico futuro.

Isso evita reenviar a mesma ordem repetidamente até o ATR "por acaso" ficar grande o suficiente — hoje o sistema não aprende nada com a rejeição, só tenta de novo no próximo candle sem ajustar nada.

## Bug 2 (a investigar) — ADAUSD rejeita volume 1 com `Retcode=10014 Invalid volume`

```
17:12:41 [INF] [Execução] Abrindo BUY ADAUSD | Vol=1 SL=0.16996 TP=0.17496 | Terminal=broker-a
17:12:41 [WRN] [Execução] ❌ Ordem recusada — Retcode=10014 Msg=Invalid volume

17:13:48 [INF] [Execução] Abrindo BUY ADAUSD | Vol=1 SL=0.17011 TP=0.17511000000000002 | Terminal=broker-a
17:13:48 [WRN] [Execução] ❌ Ordem recusada — Retcode=10014 Msg=Invalid volume
```

Volume 1 funcionou para BTCUSD, BTCLTC e BTCETH (todos abriram com sucesso), mas foi rejeitado duas vezes para ADAUSD especificamente. Não há histórico de negociação bem-sucedida em ADAUSD nesta conta (`get_deals` para o símbolo retornou vazio), então não dá para inferir o volume correto por tentativa e erro passada — precisa ser investigado, não é uma correção óbvia.

### Como investigar/corrigir

1. Consultar `SYMBOL_VOLUME_MIN`, `SYMBOL_VOLUME_MAX` e `SYMBOL_VOLUME_STEP` para `ADAUSD` via `SymbolInfoDouble` do gateway MT5 (o mesmo padrão já usado — ou a ser usado — pelo `CalculadoraLote` da Fase 3, ver `spec_fase3_strategy_engine_risco.md`, seção "Cálculo de volume").
2. Comparar o volume mínimo/step reais contra o `loteFixo: 1` configurado em `ADAUSD.config.json` — se o mínimo real for diferente (maior, menor, ou com step fracionário incompatível com `1`), ajustar o `loteFixo` no config para um valor válido.
3. Isso reforça a importância do item já pendente na Fase 3 (`CalculadoraLote` respeitando `SYMBOL_VOLUME_MIN`/`MAX`/`STEP` por símbolo, arredondando para o step válido) — se essa validação já estivesse implementada e ativa antes de enviar a ordem, esse erro teria sido pego e corrigido automaticamente (ou logado com clareza) antes de gastar uma tentativa de execução real contra a corretora.

## Critério de aceite

- [ ] BTCUSD (ou qualquer símbolo com `stopLossAtrMultiplo`) não recebe mais `Retcode=10016 Invalid stops` quando o ATR do momento gera uma distância menor que o mínimo da corretora — o sistema ajusta ou aborta de forma controlada, com log claro.
- [ ] Causa raiz do `Retcode=10014 Invalid volume` do ADAUSD identificada (valores reais de `SYMBOL_VOLUME_MIN/MAX/STEP` consultados e logados) e o `ADAUSD.config.json` corrigido com um `loteFixo` válido, ou o `CalculadoraLote` passa a arredondar automaticamente para o step correto antes de enviar.
- [ ] Uma nova ordem de teste em ADAUSD abre com sucesso (confirmável via `get_all_positions`/`get_deals` no MetaTrader).
