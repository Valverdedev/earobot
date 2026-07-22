# Spec - Comites Mini Indice com Codex local

## Objetivo

Estruturar comites manuais para WINQ26 usando definicoes JSON e um command PowerShell, mantendo contrato compativel com futura integracao no Worker.

O command nao opera, nao promove config e nao altera `D:\SistemEarobot\config`. Ele cria uma run auditavel com prompts e saidas por tarefa.

## Arquivos

- Definicoes:
  - `.agents/committees/win-pre-abertura.json`
  - `.agents/committees/win-meio-dia.json`
  - `.agents/committees/win-pos-pregao.json`
- Command:
  - `.agents/scripts/Invoke-WinCommittee.ps1`
- Artefatos gerados:
  - `.agents/runs/committees/<SYMBOL>/<yyyy-MM-dd>/<committee>-<timestamp>/`

## Uso manual seguro

Gerar prompts sem executar agente:

```powershell
& .\.agents\scripts\Invoke-WinCommittee.ps1 -Comite pre-abertura
& .\.agents\scripts\Invoke-WinCommittee.ps1 -Comite meio-dia
& .\.agents\scripts\Invoke-WinCommittee.ps1 -Comite pos-pregao
```

Com contexto adicional:

```powershell
& .\.agents\scripts\Invoke-WinCommittee.ps1 `
  -Comite pre-abertura `
  -ExtraContext "Usar somente dados extraidos hoje; nao promover config."
```

## Execucao com comando local

O parametro `-Execute` exige `-AgentCommand`. O template recebe:

- `{promptFile}`: arquivo de prompt gerado para a task.
- `{outputFile}`: arquivo onde a resposta deve ser gravada.

Exemplo conceitual:

```powershell
& .\.agents\scripts\Invoke-WinCommittee.ps1 `
  -Comite pre-abertura `
  -Execute `
  -AgentCommand 'codex-local --prompt "{promptFile}" --output "{outputFile}"'
```

O comando real depende do CLI local escolhido. Sem `-Execute`, nenhum agente externo e chamado.

## Contrato para o Worker futuro

O Worker deve poder reaproveitar os mesmos JSONs:

1. Carregar `committee.json`.
2. Validar `agents`, `tasks` e `dependsOn`.
3. Criar `run.json`.
4. Executar tasks por camadas topologicas.
5. Gravar uma saida por task em `outputs/<taskId>.md`.
6. Gravar decisao final em `outputs/decisao_final.md` e, quando possivel, `outputs/final.json`.

O Worker nao deve usar a decisao do comite como permissao direta de ordem. A decisao e insumo de governanca para `StrategyEngine`, `RiskEngine` e eventual geracao de config candidato.

## Gates permanentes

- Config ativo nunca deve ser alterado pelo command.
- Promocao para `D:\SistemEarobot\config` requer skill propria e autorizacao explicita.
- Em conta real, exigir segunda confirmacao antes de qualquer fluxo que possa causar hot reload.
- Se dados estiverem incompletos, a saida deve favorecer `nao_operar`, `pausar` ou `operar_reduzido`.
