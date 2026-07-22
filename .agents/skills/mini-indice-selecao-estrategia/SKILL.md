---
name: mini-indice-selecao-estrategia
description: Selecao auditavel de estrategia registrada para Mini Indice/WIN. Use ao escolher entre CruzamentoEma, PriceActionSuporteResistencia, OpeningRangeBreakout, ReversaoRange, ScalperWinPullbackCurto, HydrusEmaChannelBreakout, MicroTendenciaPullbackEma ou nenhuma estrategia com base no regime e catalogo.
---

# Mini Indice Selecao de Estrategia

## Objetivo

Escolher uma estrategia existente do catalogo do robo ou recomendar `nenhuma`. A skill deve respeitar implementacao real, limitacoes e bloqueios conhecidos.

## Leitura obrigatoria

Ler `D:\SistemEarobot\financial.robot\docs\catalogo-estrategias.md` antes de selecionar estrategia.

## Metodo

1. Receber regime, price action, contexto macro, correlatos e risco.
2. Escolher somente estrategia registrada no catalogo.
3. Preferir uma unica estrategia por janela operacional.
4. Explicar por que as alternativas principais foram rejeitadas.
5. Se a tese for fraca, recomendar `nenhuma` ou `operar=false`.

## Mapeamento inicial

- Tendencia nascendo: `CruzamentoEma` ou `HydrusEmaChannelBreakout`.
- Tendencia definida com pullback: `MicroTendenciaPullbackEma` ou `ScalperWinPullbackCurto` se seus bloqueios estiverem resolvidos.
- Abertura direcional: `OpeningRangeBreakout`, somente com range valido e controle de falso rompimento.
- Nivel tecnico com reteste: `PriceActionSuporteResistencia` modo reteste.
- Range maduro: `ReversaoRange`.
- Mercado indefinido, evento ou contradicao forte: `nenhuma`.

## Saida esperada

```json
{
  "skill": "mini-indice-selecao-estrategia",
  "estrategia": "CruzamentoEma|PriceActionSuporteResistencia|OpeningRangeBreakout|ReversaoRange|ScalperWinPullbackCurto|HydrusEmaChannelBreakout|MicroTendenciaPullbackEma|nenhuma",
  "modo": "",
  "operar": false,
  "motivos": [],
  "estrategias_rejeitadas": [],
  "bloqueios": [],
  "parametros_sugeridos": {}
}
```

## Regras

- Nao inventar estrategia.
- Nao ignorar limitacoes do catalogo.
- Nao aumentar risco ou lote; isso pertence a skill de risco operacional.
