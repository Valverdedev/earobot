# Plano técnico — Hermes → VPS → SignalR → financial.robot

## Objetivo

Substituir integrações de chat como Telegram por uma ponte própria na VPS, onde o Hermes Agent analisa extrações de mercado e a Bridge.Api envia atualizações JSON ao `financial.robot` local via SignalR.

O fluxo alvo é totalmente automatizado:

```text
extração/evento/cron → Bridge.Api → Hermes API Server → config.apply → SignalR → robô local → backup → aplica config → ACK
```

`operar: true` pode permanecer ativo no modo automático. Isso significa que o robô está autorizado a operar **somente depois** de validações locais, guardrails, backup e possibilidade de rollback.

## Componentes

### 1. Hermes Agent na VPS

Rodar com API Server local, não exposto publicamente:

```env
API_SERVER_ENABLED=true
API_SERVER_HOST=127.0.0.1
API_SERVER_PORT=8642
API_SERVER_KEY=<chave-forte>
```

A Bridge.Api chama preferencialmente:

- `POST /v1/responses` para MVP síncrono;
- `POST /v1/runs` + `GET /v1/runs/{run_id}/events` para streaming/progresso.

### 2. Bridge.Api na VPS

Responsabilidades:

- receber extrações via webhook;
- persistir extrações, respostas e ACKs;
- chamar o Hermes API Server;
- exigir saída JSON válida;
- validar contrato e JSON Schema;
- publicar `ConfigUpdateRequested` via SignalR;
- receber `ConfigApplyAck` do robô local;
- auditar cada transição.

Endpoints sugeridos:

```http
POST /api/chat
POST /api/extractions
POST /api/hermes/strategies
GET  /api/messages/{id}
GET  /health
```

SignalR Hub:

```text
/hubs/earobot
```

Eventos enviados ao robô:

```text
ConfigUpdateRequested
StrategyProposed
StrategyApproved
Ping
```

Eventos recebidos do robô:

```text
ConfigApplyAck
StrategyAck
StrategyRejected
RobotStatus
ExecutionReport
```

### 3. financial.robot local

Implementado nesta branch:

- contratos versionados em `Financial.Robot.Application.Automation`;
- validação de extrações e `config.apply`;
- `ConfigApplyService` com backup antes de gravação;
- cliente SignalR opcional `AutomationSignalRClientService`;
- configuração `AutomationBridge` desabilitada por padrão.

Configuração local:

```json
"AutomationBridge": {
  "Enabled": true,
  "HubUrl": "https://vps.seudominio.com/hubs/earobot",
  "DeviceToken": "<token-do-dispositivo>",
  "ConfigRootPath": "../.."
}
```

## Contratos principais

### market.extraction

Recebido pela Bridge.Api para análise automática:

```json
{
  "schemaVersion": "1.0",
  "extractionId": "ext_01JXYZ",
  "source": "financial.robot",
  "createdAtUtc": "2026-07-22T18:45:00Z",
  "trigger": {
    "type": "order_closed",
    "ticket": 123456,
    "result": "loss",
    "pnl": -120.5,
    "closeReason": "stop_loss"
  },
  "target": {
    "terminalId": "mt5-principal",
    "symbol": "WINQ26",
    "brokerSymbol": "WINQ26",
    "environment": "live",
    "timeframes": ["M5", "M1"]
  },
  "marketContext": {},
  "raw": {
    "candles": [],
    "indicators": {},
    "positions": [],
    "orders": []
  }
}
```

Gatilhos suportados no contrato:

```text
schedule
manual
order_opened
order_closed
position_updated
price_above
price_below
level_touched
spread_changed
volatility_changed
trend_changed
drawdown_warning
daily_target_reached
daily_loss_reached
market_open
market_close
```

### config.apply

Enviado pela Bridge.Api ao robô local:

```json
{
  "schemaVersion": "1.0",
  "messageId": "msg_01",
  "correlationId": "ext_01JXYZ",
  "type": "config.apply",
  "target": {
    "terminalId": "mt5-principal",
    "symbol": "WINQ26",
    "brokerSymbol": "WINQ26",
    "environment": "live",
    "timeframes": ["M5", "M1"]
  },
  "files": [
    {
      "path": "config/WINQ26.config.json",
      "operation": "merge",
      "content": {
        "operar": true,
        "estrategias": []
      }
    },
    {
      "path": "config/WINQ26-contexto-atual.estrategia.json",
      "operation": "replace",
      "content": {
        "$schemaVersion": "1.0",
        "nome": "WINQ26ContextoAtual",
        "descricao": "Estratégia gerada automaticamente após extração.",
        "filtros": [],
        "compra": {},
        "venda": {}
      }
    }
  ],
  "applyMode": "automatic",
  "rollback": {
    "enabled": true,
    "backupId": "auto"
  }
}
```

Regras locais:

- `path` deve ser relativo e ficar dentro de `config/`;
- somente `*.config.json` e `*.estrategia.json` são aceitos;
- path traversal é rejeitado;
- arquivos `.estrategia.json` exigem `$schemaVersion: "1.0"`;
- antes de sobrescrever, o robô cria backup em `config/.backups/<backupId>/`;
- operação `replace` substitui o arquivo;
- operação `merge` faz merge recursivo para objetos JSON.

### config.apply.ack

Retornado pelo robô via SignalR:

```json
{
  "schemaVersion": "1.0",
  "messageId": "ack_msg_01",
  "correlationId": "ext_01JXYZ",
  "type": "config.apply.ack",
  "status": "applied",
  "receivedAtUtc": "2026-07-22T18:51:00Z",
  "appliedAtUtc": "2026-07-22T18:51:00Z",
  "backupId": "20260722_185100_ext_01JXYZ",
  "errors": []
}
```

## Fluxo automático completo

```text
1. Ordem encerra, preço cruza nível ou cron dispara
2. financial.robot/coletor gera extração
3. POST /api/extractions na Bridge.Api
4. Bridge.Api persiste extração
5. Bridge.Api chama Hermes
6. Hermes retorna analysis.no_change, strategy.proposal, config.update ou config.replace
7. Bridge.Api converte resposta válida em config.apply
8. Bridge.Api publica ConfigUpdateRequested via SignalR
9. financial.robot valida o envelope
10. financial.robot cria backup
11. financial.robot grava config/estratégia
12. ConfigWatcher/hot-reload processa alteração
13. financial.robot envia ConfigApplyAck
14. Bridge.Api audita status final
```

## Cron e gatilhos condicionais

### Cron do Hermes

Pode ser usado para análises programadas quando o Hermes precisa buscar ou consolidar contexto:

```text
schedule: every 5m
prompt: consulte a última extração de WINQ26, analise e retorne JSON válido.
```

### Cron/timer local recomendado

Para candles, indicadores e posições reais, a fonte preferida é o próprio `financial.robot` ou um coletor local, porque ele já conversa com o MT5 e conhece o estado operacional.

### Gatilhos condicionais

- `order_closed`: reavaliar após gain/loss/parcial;
- `price_above`: ativar rompimento ou pullback comprador;
- `price_below`: ativar continuação de baixa ou desativar compra;
- `drawdown_warning`: reduzir agressividade;
- `volatility_changed`: ajustar ATR/SL/TP/filtros;
- `trend_changed`: trocar viés e arquivo `.estrategia.json`.

## Guardrails obrigatórios

Mesmo com `operar: true` e `applyMode: automatic`:

- exigir Stop Loss;
- validar path e schema localmente;
- aplicar somente symbols/terminais esperados;
- criar backup antes de gravação;
- registrar ACK/rejeição;
- permitir rollback;
- preservar travas existentes (`RiskGuard`, drawdown, horário, max operações, spread).

## Próximos passos fora deste repo

A Bridge.Api ainda precisa ser criada na VPS com:

- ASP.NET Core Web API;
- SignalR Hub `/hubs/earobot`;
- cliente HTTP para Hermes API Server;
- validação JSON Schema;
- banco SQLite/PostgreSQL para auditoria;
- autenticação por device token;
- endpoints `/api/extractions` e `/api/chat`.
