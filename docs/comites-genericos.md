# Comites genericos por ativo

Este fluxo cria uma run de comite a partir de um ativo informado pelo operador. Ele nao altera config ativo por conta propria; gera prompts, outputs e uma decisao estruturada para amadurecer a configuracao.

## Command

```powershell
& .\.agents\scripts\Invoke-AssetCommittee.ps1 -Ativo WINQ26 -Classe Auto -Objetivo configurar-estrategia
```

## Parametros principais

- `-Ativo`: simbolo alvo, por exemplo `WINQ26`, `PETR4`, `EURUSD`, `BTCUSD`.
- `-Classe`: `Auto`, `B3`, `FOREX`, `CRYPTO` ou `STOCK`.
- `-Objetivo`: `configurar-estrategia`, `reavaliar`, `auditar`, `pre-abertura`, `meio-dia` ou `pos-pregao`.
- `-ConfigPath`: caminho do config ativo quando o nome nao seguir `D:\SistemEarobot\config\<Ativo>.config.json`.
- `-Terminal`: terminal preferido/informado, por exemplo `genial` ou `activtraders`.
- `-ExtraContext`: contexto do operador, incluindo autorizacoes ou bloqueios.

## Exemplos

### WINQ26 / B3

```powershell
& .\.agents\scripts\Invoke-AssetCommittee.ps1 `
  -Ativo WINQ26 `
  -Classe B3 `
  -Objetivo configurar-estrategia `
  -Terminal genial `
  -ExtraContext "Validar PriceActionBarByBar em demo. Nao promover sem nova autorizacao."
```

### PETR4 / acao

```powershell
& .\.agents\scripts\Invoke-AssetCommittee.ps1 `
  -Ativo PETR4 `
  -Classe STOCK `
  -Objetivo pre-abertura `
  -Terminal genial
```

### EURUSD / Forex

```powershell
& .\.agents\scripts\Invoke-AssetCommittee.ps1 `
  -Ativo EURUSD `
  -Classe FOREX `
  -Objetivo reavaliar `
  -Terminal activtrades
```

### BTCUSD / Crypto

```powershell
& .\.agents\scripts\Invoke-AssetCommittee.ps1 `
  -Ativo BTCUSD `
  -Classe CRYPTO `
  -Objetivo configurar-estrategia `
  -ExtraContext "Validacao demo, considerar 24/7 e volatilidade extrema."
```

## Comites criados

- `.agents/committees/generic-b3-config.json`
- `.agents/committees/generic-forex-config.json`
- `.agents/committees/generic-crypto-config.json`
- `.agents/committees/generic-stock-config.json`

## Saida esperada

Cada run fica em:

```text
.agents\runs\committees\<Ativo>\<yyyy-MM-dd>\<comite>-<objetivo>-<timestamp>
```

Arquivos principais:

- `committee.json`: definicao do comite usada na run.
- `run.json`: metadados da execucao.
- `prompts\*.prompt.md`: prompts de cada agente.
- `outputs\*.md`: respostas esperadas por task.
- `outputs\decisao_final.md`: consolidacao do comite.
- `outputs\final.json`: decisao estruturada.

## Regra operacional

Por padrao, o comite generico prepara candidato com `operar=false`. Promover para `D:\SistemEarobot\config` ou ligar `operar=true` continua exigindo autorizacao explicita na conversa atual.
