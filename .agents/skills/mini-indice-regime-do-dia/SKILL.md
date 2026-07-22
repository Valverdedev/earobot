---
name: mini-indice-regime-do-dia
description: Classificacao do regime intradiario do Mini Indice/WIN. Use ao decidir se o dia favorece tendencia, range, abertura volatil, falso rompimento, baixa liquidez, alta volatilidade, reversao, pullback ou pausa operacional.
---

# Mini Indice Regime do Dia

## Objetivo

Classificar o ambiente operacional do WIN para orientar estrategia, risco e permissao de operar. Esta skill consolida sinais macro, price action e correlatos.

## Regimes

Usar uma das classes principais:

- `tendencia_alta`
- `tendencia_baixa`
- `range`
- `abertura_volatil`
- `falso_rompimento`
- `alta_volatilidade`
- `baixa_volatilidade`
- `baixa_liquidez`
- `transicao`
- `evitar`

## Metodo

1. Reunir evidencias tecnicas, macro e intermercado.
2. Verificar se o regime e consistente nos timeframes relevantes.
3. Identificar se o mercado esta adequado para operacao automatizada.
4. Definir validade temporal do regime.
5. Indicar gatilhos de mudanca.

## Saida esperada

```json
{
  "skill": "mini-indice-regime-do-dia",
  "regime": "tendencia_alta|tendencia_baixa|range|abertura_volatil|falso_rompimento|alta_volatilidade|baixa_volatilidade|baixa_liquidez|transicao|evitar",
  "evidencias": [],
  "contradicoes": [],
  "validade_ate": "",
  "gatilhos_de_mudanca": [],
  "operabilidade": "operavel|operavel_com_reducao|pausar|evitar",
  "confianca": 0.0
}
```

## Regras

- Nao forcar regime quando as evidencias forem mistas.
- `evitar` e uma decisao valida.
- Se houver evento iminente de alto impacto, reduzir confianca ou bloquear.
