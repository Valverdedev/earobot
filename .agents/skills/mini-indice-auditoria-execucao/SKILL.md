---
name: mini-indice-auditoria-execucao
description: Auditoria pos-pregao da execucao do Mini Indice/WIN. Use ao revisar trades reais, aderencia a tese, entradas fora da regra, saidas cedo/tarde, stop mal posicionado, reentrada indevida, overtrading, horario, spread, slippage e qualidade operacional.
---

# Mini Indice Auditoria de Execucao

## Objetivo

Avaliar se o resultado do dia veio de boa estrategia, sorte, erro de regime ou falha operacional. Esta skill apoia o comite pos-pregao.

## Entradas

Ler:

- plano pre-abertura e validacao do meio-dia;
- historico de trades do dia;
- logs do robo;
- candles e contexto dos momentos de entrada/saida;
- config ativo e limites configurados.

## Metodo

1. Classificar cada trade: aderente, parcialmente aderente ou fora da tese.
2. Identificar entrada atrasada, entrada antecipada, saida cedo, saida tarde, stop inadequado e reentrada indevida.
3. Medir overtrading: quantidade, frequencia, cooldown e trades apos invalidacao.
4. Separar erro de estrategia, erro de execucao, erro de config e condicao adversa.
5. Propor aprendizado ou ajuste candidato sem promover automaticamente.

## Saida esperada

```json
{
  "skill": "mini-indice-auditoria-execucao",
  "qualidade_execucao": "alta|media|baixa|inconclusiva",
  "trades_auditados": [],
  "erros_principais": [],
  "acertos_principais": [],
  "overtrading": false,
  "ajustes_candidatos": {},
  "manter_config_atual": true
}
```

## Regras

- Nao julgar trade apenas pelo PnL.
- Uma operacao vencedora fora da regra continua sendo falha de disciplina.
- Uma operacao perdedora aderente pode ser aceitavel se o risco estava correto.
