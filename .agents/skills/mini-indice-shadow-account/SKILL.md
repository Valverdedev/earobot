---
name: mini-indice-shadow-account
description: Analise Shadow Account para Mini Indice/WIN. Use ao comparar trades reais contra uma versao disciplinada da estrategia, estimar PnL perdido por ruido, overtrading, saida cedo, saida tarde, entrada perdida e aderencia comportamental.
---

# Mini Indice Shadow Account

## Objetivo

Criar uma comparacao contrafactual entre o que foi executado e o que uma versao disciplinada da estrategia teria feito. Usar somente para pesquisa e auditoria; nunca para emitir ordem diretamente.

## Pre-condicoes

Exigir:

- historico suficiente de trades;
- tese/config usada no periodo;
- candles para reconstruir contexto;
- regras da estrategia ou config do dia.

Se a amostra for pequena ou incompleta, retornar `insuficiente`.

## Metodo

1. Definir a versao disciplinada: entradas permitidas, saidas, horarios, cooldown e risco.
2. Comparar trades reais contra essa versao.
3. Atribuir delta de PnL por categorias:
   - `noise_trades_pnl`
   - `early_exit_pnl`
   - `late_exit_pnl`
   - `overtrading_pnl`
   - `missed_signals_pnl`
4. Listar os principais contrafactuais.
5. Gerar recomendacao de disciplina/config, sem promocao automatica.

## Saida esperada

```json
{
  "skill": "mini-indice-shadow-account",
  "status": "ok|insuficiente|inconclusivo",
  "real_total_pnl": 0,
  "shadow_total_pnl": 0,
  "delta_pnl": 0,
  "attribution": {
    "noise_trades_pnl": 0,
    "early_exit_pnl": 0,
    "late_exit_pnl": 0,
    "overtrading_pnl": 0,
    "missed_signals_pnl": 0
  },
  "top_contrafactuais": [],
  "recomendacoes": []
}
```

## Regras

- Nao fabricar sinais que nao possam ser reconstruidos.
- Nao usar resultado contrafactual como garantia de futuro.
- Nao substituir auditoria de risco por otimizacao retrospectiva.
