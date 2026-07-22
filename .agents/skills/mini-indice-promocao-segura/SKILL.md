---
name: mini-indice-promocao-segura
description: Checklist de promocao segura de config do Mini Indice/WIN para D:\SistemEarobot\config. Use somente quando houver autorizacao explicita para promover config candidato, validando conta real/demo, terminal, simbolo, risco, backup, atomicidade, hot reload, posicoes abertas e reversibilidade.
---

# Mini Indice Promocao Segura

## Objetivo

Promover um config candidato para o diretoria ativo somente com autorizacao explicita e gates completos. Esta skill existe para evitar que analise vire ordem acidental.

## Pre-condicoes

Exigir:

- autorizacao explicita nesta execucao;
- caminho do config candidato;
- config ativo atual;
- terminal, conta, modo, titular e simbolo confirmados;
- estado de posicoes abertas e risco atual.

Em conta real, exigir segunda confirmacao textual citando conta/simbolo e reconhecendo que hot reload pode afetar ordens.

## Gates

Bloquear se qualquer item falhar:

1. JSON valido e compativel com o schema esperado.
2. Estrategia registrada no catalogo.
3. Simbolo, terminal, conta e modo confirmados.
4. Sem posicao ou condicao que torne hot reload inseguro.
5. Risco, lote, SL/TP, horario, spread e cooldown validados.
6. Sem evento iminente, rollover, stale data ou divergencia material.
7. Backup timestampado criado.
8. Promocao atomica possivel e reversivel.

## Saida esperada

```json
{
  "skill": "mini-indice-promocao-segura",
  "promovido": false,
  "ativo_path": "",
  "backup_path": "",
  "candidato_path": "",
  "gates_aprovados": [],
  "gates_bloqueados": [],
  "rollback": ""
}
```

## Regras

- Nao promover sem autorizacao explicita.
- Nao aumentar risco sem autorizacao explicita adicional.
- Se houver qualquer duvida operacional, manter config ativo intacto.
