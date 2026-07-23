# Teste local — Bridge.Api + SignalR + Hermes fake

Este roteiro deixa a estrutura pronta para teste sem depender do Hermes real nem do MT5. Ele valida a Bridge.Api, o webhook `/api/extractions`, o hub SignalR `/hubs/earobot` e o contrato `config.apply`.

## 1. Build e testes automáticos

```bash
cd /root/earobot
git checkout feat/hermes-vps-signalr-automation

dotnet test financial.robot.sln --configuration Release
```

Testes específicos da Bridge.Api:

```bash
dotnet test tests/Financial.Robot.Bridge.Api.Tests/Financial.Robot.Bridge.Api.Tests.csproj \
  --configuration Release
```

O teste da Bridge cobre:

- `GET /health`;
- `POST /api/extractions` com Hermes fake;
- rejeição de extração inválida;
- publicação SignalR `ConfigUpdateRequested`.

## 2. Subir a Bridge.Api localmente

Por padrão a Bridge usa Hermes fake, suficiente para teste ponta-a-ponta local:

```bash
cd /root/earobot
ASPNETCORE_URLS=http://127.0.0.1:5088 \
  dotnet run --project src/Financial.Robot.Bridge.Api/Financial.Robot.Bridge.Api.csproj \
  --configuration Release
```

Health check:

```bash
curl -s http://127.0.0.1:5088/health
```

Esperado:

```json
{"status":"ok","service":"Financial.Robot.Bridge.Api"}
```

## 3. Enviar uma extração pelo webhook

```bash
curl -s -X POST http://127.0.0.1:5088/api/extractions \
  -H 'Content-Type: application/json' \
  --data-binary @docs/samples/market-extraction.price-above.json
```

Esperado:

```json
{
  "extractionId": "ext-local-price-above-001",
  "messageId": "cfg-ext-local-price-above-001",
  "status": "config_update_requested",
  "configApply": {
    "type": "config.apply",
    "applyMode": "automatic"
  }
}
```

Consultar mensagem gerada:

```bash
curl -s http://127.0.0.1:5088/api/messages/cfg-ext-local-price-above-001
```

## 4. Publicar config.apply manual para clientes SignalR

```bash
curl -s -X POST http://127.0.0.1:5088/api/hermes/strategies \
  -H 'Content-Type: application/json' \
  --data-binary @docs/samples/config-apply.local-test.json
```

Esperado:

```json
{
  "messageId": "msg-local-config-apply-001",
  "status": "config_update_requested"
}
```

## 5. Conectar o financial.robot na Bridge.Api

Configure o Worker, em ambiente de teste/simulação:

```json
"AutomationBridge": {
  "Enabled": true,
  "HubUrl": "http://127.0.0.1:5088/hubs/earobot",
  "DeviceToken": "",
  "ConfigRootPath": "/caminho/para/raiz/do/earobot"
}
```

Ao receber `ConfigUpdateRequested`, o Worker:

1. valida o contrato;
2. cria backup em `config/.backups/`;
3. aplica o merge/replace em `config/`;
4. envia `ConfigApplyAck` para o hub.

> Cuidado: subir o Worker real pode iniciar conexão com MT5 conforme a configuração ativa. Para primeiro teste, prefira rodar apenas os testes automatizados ou um ambiente demo/simulação.

## 6. Usar Hermes real em vez do fake

Para ligar com Hermes API Server na VPS/local:

```json
"BridgeApi": {
  "UseFakeHermes": false,
  "RequireDeviceToken": true,
  "DeviceToken": "<token-do-dispositivo>",
  "HermesBaseUrl": "http://127.0.0.1:8642",
  "HermesApiKey": "<API_SERVER_KEY>",
  "HermesConversationPrefix": "earobot",
  "HermesModel": "hermes-agent"
}
```

Rodar com variáveis de ambiente:

```bash
BridgeApi__UseFakeHermes=false \
BridgeApi__RequireDeviceToken=true \
BridgeApi__DeviceToken='<token-do-dispositivo>' \
BridgeApi__HermesBaseUrl='http://127.0.0.1:8642' \
BridgeApi__HermesApiKey='<API_SERVER_KEY>' \
ASPNETCORE_URLS=http://127.0.0.1:5088 \
  dotnet run --project src/Financial.Robot.Bridge.Api/Financial.Robot.Bridge.Api.csproj \
  --configuration Release
```

Enviar webhook autenticado:

```bash
curl -s -X POST http://127.0.0.1:5088/api/extractions \
  -H 'Content-Type: application/json' \
  -H 'X-Device-Token: <token-do-dispositivo>' \
  --data-binary @docs/samples/market-extraction.price-above.json
```

## 7. Checklist de pronto para teste

- [ ] `dotnet test financial.robot.sln --configuration Release` passa.
- [ ] `GET /health` retorna `ok`.
- [ ] `POST /api/extractions` retorna `config_update_requested`.
- [ ] Cliente SignalR recebe `ConfigUpdateRequested`.
- [ ] Worker local cria backup antes de gravar config.
- [ ] `ConfigApplyAck` aparece na Bridge.Api.
- [ ] Primeiro teste real usa `environment: simulation`.
