---
name: mini-indice-validacao-tese
description: Validacao intradiaria da tese do Mini Indice/WIN, especialmente ao meio-dia. Use ao comparar plano pre-abertura com mercado realizado, trades executados, candles, correlatos, PnL, risco restante e decidir manter, reduzir, trocar estrategia, pausar ou invalidar a tese.
---

# Mini Indice Validacao de Tese

## Objetivo

Decidir se a tese da manha continua valida. Esta skill e desenhada para o comite do meio-dia e deve ser dura contra teimosia operacional.

## Entradas

Ler:

- relatorio/plano da pre-abertura;
- candles e estado atual do WIN;
- trades executados, PnL, stops e posicoes abertas;
- correlatos e eventos ocorridos;
- config ativo e limites de risco.

## Metodo

1. Comparar cada premissa do plano com o realizado.
2. Classificar a tese como `confirmada`, `parcial`, `invalidada` ou `inconclusiva`.
3. Separar perda por tese errada, execucao ruim, regime ruim ou ruido aceitavel.
4. Recomendar acao: manter, reduzir risco, trocar estrategia, pausar ou virar vies.
5. Definir proxima revisao e gatilhos antecipados.

## Saida esperada

```json
{
  "skill": "mini-indice-validacao-tese",
  "status_tese": "confirmada|parcial|invalidada|inconclusiva",
  "acao": "manter|reduzir_risco|trocar_estrategia|pausar|virar_vies",
  "evidencias": [],
  "falhas_da_tese": [],
  "falhas_de_execucao": [],
  "risco_restante": "adequado|limitado|esgotado|desconhecido",
  "proxima_revisao": ""
}
```

## Regras

- Nao defender a tese original por inercia.
- Se o plano nao tiver premissas rastreaveis, classificar como `inconclusiva` e reduzir risco.
- `pausar` deve ser preferido quando tese e execucao estiverem ruins simultaneamente.
