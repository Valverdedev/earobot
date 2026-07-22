---
name: mini-indice-risco-operacional
description: Controle de risco operacional para Mini Indice/WIN. Use ao definir ou auditar lote, limite diario, perda restante, maximo de trades, cooldown, spread, SL/TP, stops consecutivos, horarios, exposicao e bloqueios antes de permitir config candidato.
---

# Mini Indice Risco Operacional

## Objetivo

Impedir que uma boa tese vire execucao ruim. Esta skill define limites e bloqueios operacionais para o WIN, sem aumentar risco automaticamente.

## Checagens obrigatorias

Validar:

- conta, terminal, simbolo, modo e titular;
- contrato ativo, vencimento e rollover;
- spread, stops level, tick size, digits e volume minimo;
- posicoes abertas e exposicao agregada;
- perda diaria atual, drawdown, stops consecutivos e cooldown;
- horario permitido e eventos proximos.

## Metodo

1. Calcular se o risco restante permite operar.
2. Definir `operar=false` quando qualquer gate critico falhar.
3. Recomendar reducao de risco quando tese for parcial, volatilidade alta ou execucao ruim.
4. Bloquear operacao perto de evento sem absorcao de preco.
5. Registrar limite de perda restante e validade da autorizacao.

## Saida esperada

```json
{
  "skill": "mini-indice-risco-operacional",
  "permissao": "liberado|liberado_reduzido|bloqueado",
  "motivos": [],
  "limite_perda_restante": 0,
  "max_trades_restantes": 0,
  "cooldown_recomendado_minutos": 0,
  "bloqueios": [],
  "ajustes_risco": {}
}
```

## Regras

- Nao aumentar lote, drawdown ou limite de trades sem autorizacao explicita.
- Em conta real, exigir segunda confirmacao antes de qualquer promocao que possa acionar ordens.
- Se hot reload for inseguro, bloquear promocao.
