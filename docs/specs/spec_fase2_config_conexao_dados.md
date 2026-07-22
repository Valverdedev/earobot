# Spec para IA codar — Fase 2: Config Schema + Config Watcher + Connection Manager Multi-Terminal + Market Data Service

> Pré-requisito: MVP "Modo Teste" (ver `spec_modo_teste_mt5.md`) já concluído e validado — conexão única MT5, execução de ordem/modificação/fechamento/pendente confirmadas no terminal real. Esta fase constrói a fundação de dados sobre a qual o Strategy Engine (fase seguinte) vai operar. **Não inclui lógica de decisão de compra/venda ainda.**

---

## 1. Objetivo desta fase

1. Fechar o schema definitivo do `SymbolConfig` (arquivo de configuração por ativo).
2. Implementar o Config Watcher: hot-reload de config a partir da pasta compartilhada, com validação e auditoria de mudanças.
3. Generalizar o Connection Manager do modo teste (hoje 1 conexão fixa) para suportar **N terminais MT5 simultâneos**, cada um numa corretora/conta diferente.
4. Implementar o Market Data Service: ingestão de tick/quote de cada terminal conectado, publicada num canal interno.

## 2. Decisões de design já fechadas nesta fase (resolvendo pendências do documento de arquitetura)

**Formato do arquivo de config: JSON.** Motivo: nativo no .NET (`System.Text.Json`, já usado no modo teste), mais fácil de gerar a partir dos relatórios de análise (LLM já produz JSON bem) do que INI.

**Um arquivo por ativo, não tabela central.** Cada símbolo tem seu próprio arquivo de config (ex.: `config/EURUSD.config.json`). Motivo: mais fácil de versionar/auditar isoladamente, e alinhado com a convenção já usada nos relatórios (`relatorios/{ATIVO}/{DATA}/`).

**O que acontece quando o config muda com posição aberta:** por padrão, mudanças de config valem **apenas para novas decisões de entrada** — uma posição já aberta continua com o SL/TP/regras de saída que tinha no momento em que foi aberta. Só se o novo config trouxer `"applyToOpenPositions": true` explicitamente é que os níveis de saída (`encerrarAtivaAcimaDe`/`encerrarAtivaAbaixoDe`, SL/TP) devem ser reaplicados a posições já abertas daquele símbolo. Isso evita comportamento errático no meio de uma operação (preocupação já levantada antes), mas permite um "modo emergência" quando necessário (ex.: revisão de calibração aponta que a operação atual deveria ser encerrada).

## 3. Schema do `SymbolConfig` (definitivo para esta fase)

```json
{
  "$schemaVersion": "1.0",
  "symbol": "EURUSD",
  "brokerSymbol": "EURUSD",
  "terminalId": "broker-a",
  "geradoEm": "2026-07-10T21:25:00Z",
  "origemRelatorio": "relatorios/EURUSD/2026-07-10/update_2125_noticias_tecnica.md",

  "operar": true,
  "comprar": true,
  "vender": false,

  "entrada": {
    "comprarAcimaDe": 1.14500,
    "comprarAbaixoDe": null,
    "venderAcimaDe": null,
    "venderAbaixoDe": null
  },

  "saida": {
    "encerrarAtivaAcimaDe": 1.15000,
    "encerrarAtivaAbaixoDe": 1.14000,
    "stopLossPips": 30,
    "takeProfitPips": 50,
    "trailingStopPips": 15,
    "applyToOpenPositions": false
  },

  "gestaoDeRisco": {
    "modoLote": "percentualConta",
    "loteFixo": 0.01,
    "percentualConta": 1.0,
    "maxOperacoesSimultaneas": 1,
    "drawdownDiarioMaximoPercent": 3.0,
    "janelaHorarioPermitido": {
      "inicioUtc": "07:00",
      "fimUtc": "17:00"
    }
  },

  "indicadores": [
    { "nome": "RSI", "timeframe": "H1", "parametros": { "periodo": 14 } },
    { "nome": "EMA", "timeframe": "D1", "parametros": { "periodo": 20 } },
    { "nome": "ATR", "timeframe": "H1", "parametros": { "periodo": 14 } }
  ],

  "confiancaSinal": "media",
  "perfil": "scalper"
}
```

Notas de campo:
- `terminalId` referencia a conexão configurada no Connection Manager (ver seção 5) — obrigatório, sem default.
- `brokerSymbol` existe separado de `symbol` porque o nome pode divergir por corretora (ex.: `EURUSDmicro`).
- `indicadores` é uma lista aberta — cada entrada referencia um indicador pelo nome (deve existir uma implementação `IIndicator` correspondente registrada no catálogo, conforme decidido na arquitetura). Não é escopo desta fase implementar os indicadores em si, só o schema que os referencia.
- `perfil` (`scalper`/`swing`/`longTrader`) é informativo por enquanto, pode ganhar comportamento diferenciado em fase futura.

## 4. Config Watcher

- `FileSystemWatcher` na pasta `config/` (path configurável via `appsettings.json`), filtrando `*.config.json`.
- Debounce de eventos: o `FileSystemWatcher` do .NET dispara múltiplos eventos para uma única gravação — aplicar um debounce de ~500ms antes de processar.
- Ao detectar mudança: ler o arquivo, desserializar, **validar antes de aplicar**:
  - Campos obrigatórios presentes (`symbol`, `terminalId`, `operar`).
  - Coerência básica: se `comprarAcimaDe` e `comprarAbaixoDe` estiverem ambos preenchidos, isso é válido (estratégias diferentes), mas se algum nível de entrada for `null` E o campo correspondente (`comprar`/`vender`) for `true`, logar aviso (não necessariamente bloquear, mas sinalizar config incompleto).
  - `terminalId` deve existir na lista de conexões configuradas no Connection Manager — se não existir, **rejeitar o config** e manter o anterior, logando o motivo.
- Se válido: atualizar o estado em memória (`ConcurrentDictionary<string symbol, SymbolConfig>`), logar a mudança (valores antigo → novo, campo a campo se possível, ou o diff do JSON) para fins de auditoria.
- Se inválido: manter o config anterior ativo, logar erro detalhado, **não derrubar o serviço**.
- Expor um evento/callback (`ConfigChanged`) que outros módulos (Strategy Engine, na fase seguinte) possam assinar.

## 5. Connection Manager — generalização multi-terminal

Configuração de terminais via `appsettings.json` (ou arquivo próprio `terminals.config.json`):

```json
{
  "terminals": [
    { "terminalId": "broker-a", "host": "localhost", "port": 8228 },
    { "terminalId": "broker-b", "host": "localhost", "port": 8229 }
  ]
}
```

Requisitos:
- Manter `Dictionary<string terminalId, MtApi5Client>` (ou wrapper próprio em volta do client).
- Conectar todos os terminais configurados na inicialização do serviço; cada conexão é independente — falha em um terminal não deve impedir os outros de conectar.
- Health-check periódico (ex.: a cada 30s, checar `ConnectionState` de cada client) com reconexão automática em caso de queda, usando backoff (ex.: 5s, 15s, 30s, 60s, manter em 60s).
- Logar estado de cada terminal separadamente (conectado/desconectado/reconectando), sempre incluindo o `terminalId` na mensagem de log para diferenciar qual conexão é qual.
- Expor um método `GetClient(string terminalId)` usado pelo Execution Service (fase seguinte) e pelo Market Data Service.
- Reaproveitar a lógica de conexão já validada no modo teste (`Mt5ConnectionService`), generalizando para múltiplas instâncias em vez de uma fixa.

## 6. Market Data / Tick Service

- Para cada terminal conectado, assinar o evento `QuoteUpdate` do respectivo `MtApi5Client`.
- Chave de dado: `(terminalId, symbol)` — nunca só `symbol`, porque corretoras diferentes têm cotações diferentes para o mesmo par.
- Publicar cada tick recebido num canal interno (`System.Threading.Channels.Channel<TickEvent>`), com o tick normalizado: `terminalId`, `symbol`, `bid`, `ask`, `timestamp`.
- Este serviço **não decide nada** — só capta e distribui. Quem consome o canal (Strategy Engine) vem na próxima fase; nesta fase, um consumidor mínimo de log (ex.: logar 1 tick a cada N segundos por símbolo, não todos, para não afogar o log) é suficiente para validar que o fluxo funciona.
- Símbolos a assinar: apenas os presentes nos `SymbolConfig` carregados pelo Config Watcher (não assinar o terminal inteiro/todos os símbolos do Market Watch).

## 7. Estrutura de projeto (evolução da fase anterior)

```
/TradingSystem
  /src
    /TradingSystem.Worker
      /Config
        SymbolConfig.cs
        ConfigWatcherService.cs
        ConfigValidator.cs
      /Mt5
        TerminalConnection.cs        (wrapper de 1 MtApi5Client + terminalId + estado)
        ConnectionManagerService.cs  (dicionário de TerminalConnection, health-check)
      /MarketData
        TickEvent.cs
        MarketDataService.cs
      /TestMode
        (já existente, sem mudanças)
```

## 8. Critérios de aceite

- [ ] `SymbolConfig` de exemplo (`EURUSD.config.json`) carrega corretamente na inicialização.
- [ ] Editar o arquivo de config em disco reflete em memória em até poucos segundos, sem reiniciar o serviço, e o log mostra o diff da mudança.
- [ ] Config inválido (ex.: `terminalId` inexistente) é rejeitado e logado, sem derrubar o serviço nem substituir o config válido anterior.
- [ ] Serviço conecta simultaneamente a **pelo menos 2 terminais MT5** configurados em portas diferentes (pode ser 2 instâncias demo do mesmo MT5 em portas distintas para teste, não precisa ser 2 corretoras reais nesta fase de validação).
- [ ] Queda de um terminal (fechar o MT5 manualmente durante o teste) não derruba a conexão com o outro terminal, e o serviço tenta reconectar automaticamente com backoff.
- [ ] Tick de cada terminal aparece no log com o `terminalId` correto, confirmando que a chave `(terminalId, symbol)` está sendo respeitada (ex.: publicar preços diferentes propositalmente entre os dois terminais de teste, se possível, para confirmar que não há mistura).

## 9. Fora de escopo (ainda)

- Cálculo de indicadores (Skender.Stock.Indicators) — schema já referencia indicadores, mas o cálculo em si vem na próxima fase junto com o Strategy Engine.
- Decisão de compra/venda (Strategy Engine).
- Execução de ordens baseada em sinal (Execution Service orientado a estratégia — o modo teste já prova que a execução funciona, mas de forma fixa/manual, não orientada por config real).
- Persistência em banco (SQLite/EF Core) — auditoria nesta fase é só log estruturado (igual ao modo teste).
- Painel visual.
