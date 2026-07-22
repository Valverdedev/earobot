# Spec para IA codar — Serviço .NET, MVP "Modo Teste" de conexão MT5

> Este documento é a especificação para uma IA (ou dev) começar a implementação em código. Escopo deliberadamente restrito: não é o sistema de trading completo, é o primeiro pedaço executável — provar que a conexão com o MetaTrader funciona de ponta a ponta antes de qualquer lógica de estratégia.

---

## 1. Objetivo deste MVP

Criar um serviço .NET que:
1. Lê um arquivo de configuração.
2. Suporta um **modo teste**, ativado por parâmetro, que roda uma sequência de ações reais contra o MetaTrader (conta demo/teste) para validar que toda a cadeia de conexão e execução funciona.
3. Loga cada ação detalhadamente, com delay configurável entre elas.

Este MVP **não** inclui: leitura de sinais de indicador, motor de estratégia, múltiplos terminais/corretoras, hot-reload de config, painel visual. Isso vem depois — ver `trading-system/ideias/arquitetura_dotnet.md` para o desenho completo do sistema final. Este documento cobre só a fatia inicial "prova de vida" da conexão.

## 2. Contexto (resumo para quem vai codar sem ter visto o resto do projeto)

- O projeto final é um sistema de trading automatizado que opera múltiplos ativos via MetaTrader 5, com parâmetros calibrados a partir de relatórios de análise (fundamental + notícias + técnica) gerados por LLM.
- A ponte com o MT5 é feita pela biblioteca **MtApi5** (projeto open source `vdemydiuk/mtapi`, https://github.com/vdemydiuk/mtapi). Ela funciona assim: um Expert Advisor genérico (bridge) roda anexado a **qualquer** gráfico do terminal MT5 e abre um WebSocket numa porta configurável. Do lado .NET, um `MtApi5Client` conecta nessa porta e executa comandos — `Buy`, `Sell`, `PositionModify`, `PositionClose`, `OrderSend`, `CopyRates`, etc. — todos aceitando **symbol explícito**, não amarrado ao símbolo do gráfico onde o EA está.
- Isso significa: 1 EA anexado a 1 gráfico qualquer já é suficiente para operar qualquer símbolo do terminal. Não é necessário (nem desejável) escrever lógica nova em MQL5.

## 3. Pré-requisitos de ambiente (documentar/validar antes de codar)

- MetaTrader 5 instalado, com conta **demo** configurada para os testes (nunca rodar o modo teste numa conta real).
- EA bridge do MtApi5 instalado e anexado a um gráfico qualquer do terminal, escutando numa porta (padrão de exemplo: 8228). Instruções de instalação estão no README do repositório `vdemydiuk/mtapi` (usar o instalador `MtApi5Installer_x64.msi`).
- Referência ao `MtApi5.dll` (e dependências: `MtClient`, `MT5Connector`) no projeto .NET. **Atenção:** não foi confirmada a existência de um pacote NuGet oficial publicado pelo autor sob o nome exato `MtApi5` — a forma confirmada de obter os binários é compilar a solução `MetaTraderApi_2022.sln` do repositório (Visual Studio 2022, .NET 8) ou usar os binários gerados pelo instalador MSI, em `../bin/`. Antes de começar a codar, confirmar isso e, se possível, adicionar os DLLs como referência de projeto local (não assumir `dotnet add package MtApi5` até validar).
- .NET 8 SDK.

## 4. Stack

- **.NET 8**, projeto do tipo Worker Service (`Microsoft.Extensions.Hosting`) ou console app simples — usar Worker Service, já pensando na evolução para serviço de longa duração.
- **MtApi5** (`MtApi5Client`) para conexão com o terminal.
- **Serilog** (`Serilog.AspNetCore` ou `Serilog.Extensions.Hosting` + `Serilog.Sinks.File` + `Serilog.Sinks.Console`) para logging estruturado.
- **System.Text.Json** para leitura do arquivo de config (nativo, sem dependência extra).
- Configuração via `appsettings.json` + arquivo de config específico do modo teste (ver seção 6).

## 5. Estrutura de projeto sugerida

```
/TradingSystem
  /TradingSystem.sln
  /src
    /TradingSystem.Worker          (projeto principal, Worker Service / host)
      Program.cs
      appsettings.json
      /Config
        TestModeConfig.cs          (classe fortemente tipada do config de teste)
        ConfigLoader.cs            (lê e valida o JSON)
      /Mt5
        Mt5ConnectionService.cs    (encapsula MtApi5Client: conectar/desconectar/estado)
      /TestMode
        TestModeRunner.cs          (orquestra a sequência de ações do modo teste)
      /Logging
        (config do Serilog, se não for tudo no Program.cs)
  /config
    test-mode.config.json          (arquivo de config de exemplo para o modo teste)
  /logs
    (saída dos logs, gitignored)
```

## 6. Arquivo de configuração do modo teste

Localização: `config/test-mode.config.json` (caminho configurável via argumento de linha de comando ou variável de ambiente).

```json
{
  "testMode": true,
  "mtApi": {
    "host": "localhost",
    "port": 8228,
    "connectTimeoutSeconds": 15
  },
  "testSequence": {
    "symbol": "EURUSD",
    "volume": 0.01,
    "orderType": "BUY",
    "stopLossPips": 20,
    "takeProfitPips": 20,
    "modifiedStopLossPips": 30,
    "modifiedTakeProfitPips": 30,
    "pendingOrder": {
      "type": "BUY_LIMIT",
      "distancePips": 50,
      "stopLossPips": 20,
      "takeProfitPips": 20
    },
    "delayBetweenActionsSeconds": 5
  },
  "logging": {
    "filePath": "logs/test-mode-.log",
    "minimumLevel": "Information"
  }
}
```

Notas de design do config:
- Distâncias em pips (não preço absoluto) para o pedido de teste, porque o preço de mercado muda a cada execução — o serviço calcula o preço real de SL/TP/pending no momento da execução, a partir do preço atual (`SymbolInfoTick`/preço de abertura retornado pela ordem).
- `delayBetweenActionsSeconds` é único e global neste MVP (não por ação) — pode evoluir para delay por etapa se necessário depois.
- Este arquivo é **separado** do futuro `SymbolConfig` de produção (ver `arquitetura_dotnet.md`) — é só para o modo teste/diagnóstico.

## 7. Especificação funcional — Modo Teste

Ativado por argumento de linha de comando, ex.: `TradingSystem.Worker.exe --mode=test --config=config/test-mode.config.json`.

Quando `testMode: true`, o serviço executa **uma vez** a sequência abaixo e encerra (não fica em loop). Cada passo:
- Só começa após o delay configurado do passo anterior (exceto o primeiro).
- É logado **antes** de executar (log de intenção: "vou fazer X com parâmetros Y") e **depois** de executar (log de resultado: sucesso/falha, resposta completa da API, ticket/id retornado, timestamp).
- Se uma ação falhar, o serviço loga o erro com detalhe (código de erro do MT5, mensagem) e **interrompe a sequência** (não tenta continuar para as próximas ações com estado inconsistente) — loga um resumo final indicando em qual passo parou.

### Sequência (ordem exata)

1. **Conectar ao MetaTrader** — `MtApi5Client.BeginConnect(port)`, aguardar evento de conexão (sucesso/falha/timeout conforme `connectTimeoutSeconds`). Logar estado da conexão e info da conta (`AccountInfoDouble` para balance/equity, `AccountInfoString` para nome da conta) assim que conectado — confirma que não é só "conectou no socket" mas que a conta está de fato acessível.
2. **Abrir ordem a mercado** — usar os parâmetros do `testSequence` (symbol, volume, orderType, SL/TP calculados a partir de pips + preço atual). Chamar o equivalente a `Buy`/`Sell` do `MtApi5Client`. Logar o ticket retornado, preço de abertura real, SL/TP setados.
3. **Delay** (`delayBetweenActionsSeconds`).
4. **Alterar Take Profit** da posição aberta no passo 2, usando `modifiedTakeProfitPips` a partir do preço de abertura. Chamar `PositionModify(ticket, sl, tp)` mantendo o SL atual e só mudando o TP. Logar valores antes/depois.
5. **Delay**.
6. **Alterar Stop Loss** da mesma posição, usando `modifiedStopLossPips`, mantendo o TP já alterado no passo 4. Logar valores antes/depois.
7. **Delay**.
8. **Fechar a posição** — `PositionClose(ticket)`. Logar resultado (lucro/prejuízo da operação de teste, preço de fechamento).
9. **Delay**.
10. **Colocar ordem pendente** — tipo definido em `pendingOrder.type` (ex.: `BUY_LIMIT`), a uma distância de `distancePips` do preço atual, com SL/TP próprios. Logar ticket da ordem pendente e preço definido.
11. **Delay**.
12. **Cancelar a ordem pendente** criada no passo 10. Logar confirmação do cancelamento.
13. **Delay**.
14. **Desconectar** do MetaTrader (`BeginDisconnect`) e logar resumo final: lista de todos os passos executados, status de cada um (sucesso/falha), duração total da sequência.

### Requisitos de log

- Usar Serilog com sink de arquivo (rotativo diário) e console.
- Cada entrada de log deve incluir: timestamp, nome do passo (ex.: `"Step 4 - Modify TP"`), parâmetros enviados, resposta bruta da API (serializada), sucesso/falha.
- Nível `Information` para fluxo normal, `Warning` para retries, `Error` para falhas que interrompem a sequência.
- Ao final, gravar (ou logar) um resumo estruturado em JSON com todos os passos e resultados — isso facilita auditoria manual de que o teste realmente passou por todas as etapas.

## 8. Referência rápida da API MtApi5Client (já validada em pesquisa anterior)

Para orientar a implementação, os métodos abaixo já foram confirmados como existentes na biblioteca (não precisam ser redescobertos):

```csharp
// Trade
public bool Buy(out MqlTradeResult? result, double volume, string? symbol = null, double price = 0.0, double sl = 0.0, double tp = 0.0, string? comment = null)
public bool Sell(out MqlTradeResult? result, double volume, string? symbol = null, ...)
public bool PositionOpen(string symbol, ENUM_ORDER_TYPE orderType, double volume, double price, double sl, double tp, string comment = "")
public bool PositionClose(ulong ticket, ulong deviation = ulong.MaxValue)
public bool PositionModify(ulong ticket, double sl, double tp)
public bool OrderSend(MqlTradeRequest request, out MqlTradeResult? result)   // usar para ordem pendente (TRADE_ACTION_PENDING) e cancelamento (TRADE_ACTION_REMOVE)

// Dados
public int CopyRates(string symbolName, ENUM_TIMEFRAMES timeframe, int startPos, int count, out MqlRates[]? ratesArray)
public bool SymbolInfoTick(string symbol, out MqlTick? tick)
public double SymbolInfoDouble(string symbolName, ENUM_SYMBOL_INFO_DOUBLE propId)

// Conta / posições
public double AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE propertyId)
public string? AccountInfoString(ENUM_ACCOUNT_INFO_STRING propertyId)
public int PositionsTotal()
public string? PositionGetSymbol(int index)
public bool PositionSelect(string symbol)
```

Não existem métodos dedicados `OrderModify`/`OrderDelete` para ordens pendentes — usar `OrderSend` com `MqlTradeRequest.action = TRADE_ACTION_MODIFY` (alterar) ou `TRADE_ACTION_REMOVE` (cancelar), preenchendo o `ticket` da ordem pendente.

## 9. Critérios de aceite (Definition of Done deste MVP)

- [ ] Serviço compila e roda com `--mode=test` apontando para uma conta MT5 demo real.
- [ ] Todos os 14 passos da sequência executam em ordem, com delay respeitado entre eles.
- [ ] Log de arquivo contém entrada de intenção + resultado para cada passo, com timestamp.
- [ ] Se qualquer passo falhar (ex.: MT5 fechado, símbolo inválido, volume abaixo do mínimo), o serviço loga o erro claramente e para — não deixa posição "solta" sem log.
- [ ] Ao final de uma execução bem-sucedida, existe uma posição a mercado que foi aberta, teve TP alterado, teve SL alterado, e foi fechada — e uma ordem pendente que foi criada e cancelada — tudo confirmável comparando o log com o histórico de negociações do terminal (`get_deals`/`get_orders` ou aba de histórico do MT5).
- [ ] Nenhuma ordem fica aberta/pendente no terminal ao final da execução do modo teste (o teste deve se "limpar sozinho").

## 10. Fora de escopo (explicitamente, para não confundir a IA que for codar)

- Leitura de indicadores técnicos (Skender.Stock.Indicators) — decidido na arquitetura, mas não faz parte deste MVP.
- Motor de estratégia / decisão de compra e venda baseada em config real de ativo.
- Múltiplos terminais/corretoras (Connection Manager completo).
- Hot-reload de config via `FileSystemWatcher`.
- Persistência em SQLite / auditoria de longo prazo.
- Qualquer UI/painel.

Esses itens estão documentados em `trading-system/ideias/arquitetura_dotnet.md` para as próximas etapas, depois que este modo teste provar que a conexão básica funciona de ponta a ponta.
