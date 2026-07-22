# Spec para IA codar — Fase 3: Strategy Engine + Risk Management + Execution Service orientado a estratégia

> Pré-requisitos concluídos: Modo Teste (conexão/execução MT5 validada), Fase 2 (Config Watcher, Connection Manager multi-terminal, Market Data Service) e o patch de validação `slMinimoSobreSpread`/`UnidadeDistancia`. Esta fase é onde o sistema passa a **decidir** e **executar de verdade** com base em sinal, não mais em sequência fixa de teste.

---

## 1. Objetivo desta fase

1. Calcular indicadores técnicos reais (Skender.Stock.Indicators) a partir dos candles do MetaTrader, por símbolo/timeframe conforme declarado no `SymbolConfig.indicadores`.
2. Implementar o Strategy Engine: consome ticks (Market Data Service) e candles fechados, cruza com o config vigente, decide se abre/fecha operação.
3. Implementar as regras de Risk Management (a maioria já está no schema `gestaoDeRisco` desde a Fase 2 — aqui é onde elas passam a ser **aplicadas de fato**, não só armazenadas).
4. Generalizar o Execution Service: em vez da sequência fixa do Modo Teste, ele passa a executar decisões vindas do Strategy Engine.
5. Fechar a pendência deixada pelo patch da Fase 2: validar `StopLossAtrMultiplo × ATR atual` contra o spread em tempo real, antes de cada entrada.

## 2. Cálculo de indicadores (Skender.Stock.Indicators)

- Adicionar o pacote NuGet `Skender.Stock.Indicators`.
- Criar a interface `IIndicator`:
  ```csharp
  public interface IIndicator
  {
      string Nome { get; }
      IndicatorResult Calcular(IEnumerable<Quote> candles, IDictionary<string, object> parametros);
  }
  ```
  (`Quote` é o tipo de candle que o próprio Skender usa como entrada — reaproveitar, não recriar um tipo próprio.)
- Implementações mínimas nesta fase: `RsiIndicator`, `EmaIndicator`, `AtrIndicator`, `MacdIndicator` — todas usando o método de Wilder onde aplicável (já decidido na arquitetura, ver `arquitetura_dotnet.md`).
- Registrar as implementações num catálogo (`Dictionary<string, IIndicator>` por nome), resolvido a partir da lista `SymbolConfig.indicadores`.
- Buscar histórico de candles via `CopyRates` (símbolo + timeframe do `TerminalConnection` correto, via `terminalId` do config) na inicialização/reconexão de cada símbolo monitorado, quantidade suficiente para o maior período configurado (ex.: se algum indicador usa período 200, buscar pelo menos 210 candles). Manter em memória e atualizar incrementalmente a cada fechamento de candle novo (não recalcular a série inteira a cada tick).
- Definir claramente o "gatilho de fechamento de candle": para timeframe M1, por exemplo, um candle fecha a cada minuto — o serviço precisa de um mecanismo (timer ou detecção de mudança de `time` no candle mais recente vindo do `CopyRates`) para saber quando recalcular.

## 3. Strategy Engine

- Um `StrategyEngine` por símbolo ativo (`SymbolConfig.Operar == true`), instanciado/atualizado pelo `ConfigWatcherService` via o evento `ConfigChanged` já existente da Fase 2.
- Consome do `Channel<TickEvent>` do Market Data Service (filtrando por `symbol` e `terminalId` relevantes ao config).
- Lógica de decisão de entrada, nesta ordem de prioridade:
  1. Se `SymbolConfig.Entrada` tiver níveis fixos (`comprarAcimaDe`, `comprarAbaixoDe`, `venderAcimaDe`, `venderAbaixoDe`) não nulos, avaliar rompimento desses níveis contra o tick atual.
  2. Se não houver níveis fixos mas houver indicadores configurados, avaliar sinal baseado em indicador — nesta fase, implementar como regra mínima o cruzamento de médias (ex.: EMA rápida cruza EMA lenta) combinado com filtro de RSI (faixa configurável, default 40-70 para compra e 30-60 para venda, conforme já usado no backtest exploratório de BTCUSD) — isso cobre o caso do `BTCUSD.config.exemplo.json`.
  3. Se nenhuma das duas regras acima estiver configurada, símbolo fica em modo "observação apenas" (calcula e loga indicadores, mas não gera sinal) — logar isso claramente para não parecer que o motor está "travado".
- **Antes de qualquer entrada**, checar (nesta ordem, abortando a entrada e logando o motivo se qualquer uma falhar):
  1. `SymbolConfig.Operar` e a direção específica (`Comprar`/`Vender`) estão habilitados.
  2. `GestaoDeRisco.JanelaHorarioPermitido` — se não nula, o horário atual está dentro da janela.
  3. `GestaoDeRisco.MaxOperacoesSimultaneas` — não excede o número de posições abertas atuais para aquele símbolo.
  4. `GestaoDeRisco.DrawdownDiarioMaximoPercent` — se o drawdown do dia (calculado a partir do saldo inicial do dia vs. equity atual, via `AccountInfoDouble`) já estourou o limite, bloquear novas entradas até o próximo dia (mas não fechar posições existentes automaticamente).
  5. **Validação de SL vs. spread em tempo real** (pendência deixada pelo patch da Fase 2): se `SaidaConfig.StopLossAtrMultiplo` estiver definido, calcular `StopLossAtrMultiplo × ATR atual do símbolo` e comparar contra o spread atual (`SymbolInfoDouble`/tick) multiplicado por `SlMinimoSobreSpread`. Se a distância implícita do SL não superar essa margem, **abortar a entrada** e logar os valores comparados (não é erro de sistema, é a operação sendo recusada por segurança — logar como `Warning`, não `Error`).
- Sinal validado e aprovado por todas as checagens → delega ao Execution Service (seção 4).

## 4. Execution Service (generalizado)

- Reaproveita a camada de conexão já validada no Modo Teste (`Mt5ConnectionService`/`ConnectionManagerService`), mas agora orientado a comando, não a sequência fixa.
- Interface sugerida:
  ```csharp
  public interface IExecutionService
  {
      Task<OrderResult> AbrirPosicaoAsync(string terminalId, string symbol, TradeSide side, decimal volume, decimal? sl, decimal? tp);
      Task<OrderResult> FecharPosicaoAsync(string terminalId, ulong ticket);
      Task<OrderResult> ModificarPosicaoAsync(string terminalId, ulong ticket, decimal? sl, decimal? tp);
  }
  ```
- Cálculo de volume: se `GestaoDeRisco.ModoLote == "percentualConta"`, calcular o lote dinamicamente a partir de `AccountInfoDouble` (equity) e `PercentualConta`, respeitando o volume mínimo/máximo e o step do símbolo (`SymbolInfoDouble` para `SYMBOL_VOLUME_MIN`/`MAX`/`STEP`) — arredondar para o step válido, nunca enviar volume inválido para a corretora.
- Logar cada execução real com o mesmo nível de detalhe do Modo Teste (parâmetros enviados, resposta da API, ticket, sucesso/falha) — reaproveitar o padrão de log já estabelecido.
- Em caso de falha de execução (rejeição da corretora, erro de conexão), **não deixar o Strategy Engine em estado inconsistente** — se a ordem falhar, o símbolo permanece elegível para nova tentativa no próximo ciclo, não fica "travado" esperando uma confirmação que não veio.

## 5. Estrutura de projeto (evolução)

```
/TradingSystem/src/TradingSystem.Worker
  /Indicators
    IIndicator.cs
    RsiIndicator.cs
    EmaIndicator.cs
    AtrIndicator.cs
    MacdIndicator.cs
    IndicatorCatalog.cs
  /Strategy
    StrategyEngine.cs
    StrategyEngineFactory.cs      (cria/atualiza uma engine por símbolo, reage a ConfigChanged)
    EntradaDecisionResult.cs
  /Risk
    RiskGuard.cs                  (as checagens da seção 3, testáveis isoladamente)
    LotSizeCalculator.cs
  /Execution
    IExecutionService.cs
    ExecutionService.cs
    OrderResult.cs
```

## 6. Critérios de aceite

- [ ] Com `BTCUSD.config.exemplo.json` carregado, o serviço calcula RSI/EMA9/EMA21/ATR reais em M1 e loga os valores periodicamente (não a cada tick — a cada candle fechado).
- [ ] Um cruzamento de EMA válido, com RSI dentro da faixa e todas as checagens de risco aprovadas, resulta em uma ordem real aberta numa conta demo, com SL/TP calculados a partir do ATR no momento da entrada.
- [ ] Se o spread no momento subir a ponto de violar `slMinimoSobreSpread`, a entrada é recusada e logada — sem abrir ordem.
- [ ] `MaxOperacoesSimultaneas` é respeitado: com uma posição já aberta no símbolo, novo sinal não abre uma segunda se o limite for 1.
- [ ] Alterar o config em disco durante a execução (hot-reload já validado na Fase 2) reflete no comportamento do Strategy Engine daquele símbolo sem reiniciar o serviço.
- [ ] Falha de execução (ex.: símbolo travado/mercado fechado) é logada e não derruba o serviço nem trava o símbolo indefinidamente.

## 7. Fora de escopo (ainda)

- Persistência em banco (SQLite/EF Core) — log estruturado continua sendo a fonte de auditoria nesta fase.
- Trailing stop dinâmico em tempo real (campo já existe no schema, mas a lógica de acompanhar o preço e mover o SL continuamente fica para depois — nesta fase, TP/SL são fixados na abertura e só revistos se o config mudar com `applyToOpenPositions: true`).
- Painel visual.
- Múltiplas estratégias concorrentes no mesmo símbolo (por enquanto, 1 config = 1 lógica de decisão por símbolo).
