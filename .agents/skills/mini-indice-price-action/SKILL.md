---
name: mini-indice-price-action
description: Leitura tecnica e Price Action do Mini Indice/WIN. Use ao analisar candles, tendencia, range, gap, maxima/minima anterior, abertura, VWAP, suporte, resistencia, rompimento, rejeicao, pullback, volatilidade e niveis relevantes para comites do WIN.
---

# Mini Indice Price Action

## Objetivo

Mapear a estrutura tecnica do WIN sem inventar niveis. A skill deve transformar candles e dados extraidos do terminal em contexto operacional para escolha ou bloqueio de estrategia.

## Entradas

Usar preferencialmente dados do extrator/terminal e relatorios recentes. Confirmar:

- simbolo e terminal;
- timestamp dos candles e fuso;
- bid/ask, spread, tick size, digits, stops level e volume minimo;
- D1, H4, H1, M15, M5 e M1 quando disponiveis.

## Metodo

1. Ler estrutura maior: tendencia, range ou transicao.
2. Marcar maxima/minima/fechamento anterior, abertura, gap, VWAP quando disponivel e regioes de liquidez.
3. Classificar candle atual e contexto: impulso, correcao, compressao, expansao, rejeicao, rompimento, reteste ou falha.
4. Validar se o nivel ainda e atual. Niveis manuais vencidos devem ser bloqueados.
5. Diferenciar sinal tecnico de permissao operacional. Um padrao bom ainda pode ser bloqueado por risco, spread ou evento.

## Saida esperada

```json
{
  "skill": "mini-indice-price-action",
  "regime_tecnico": "tendencia_alta|tendencia_baixa|range|transicao|indefinido",
  "estrutura": "impulso|correcao|compressao|expansao|rompimento|reteste|rejeicao|falha",
  "niveis_superiores": [],
  "niveis_inferiores": [],
  "confirmacoes": [],
  "contradicoes": [],
  "bloqueios_tecnicos": [],
  "confianca": 0.0
}
```

## Regras

- Separar nivel observado de nivel inferido.
- Nao usar candle ainda aberto como se estivesse fechado.
- Nao escolher estrategia sozinho; fornecer insumos para a skill de selecao.
