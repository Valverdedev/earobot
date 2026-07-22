---
name: estrategia-opening-range-breakout
description: Especialista na estrategia OpeningRangeBreakout do financial.robot. Use ao selecionar, analisar, auditar ou configurar rompimentos da faixa de abertura, incluindo janelas horarias, timezone, lookback, risco de falso rompimento e configuracao candidata desabilitada.
---

# Especialista Opening Range Breakout

Ler integralmente `references/contrato-estrategia.md` antes de agir.

## Fluxo

1. Ler config, extracoes, ultimo relatorio e logs; confirmar fuso horario efetivo dos candles e do tick antes de usar qualquer janela.
2. Reconstruir a faixa de abertura com os candles reais e medir amplitude, participacao, direcao e retorno para dentro da faixa.
3. Selecionar ORB apenas se a abertura mostrar expansao direcional e espaco operacional depois do rompimento.
4. Verificar todos os requisitos e limitacoes do codigo, especialmente que o gatilho atual usa tick e nao fechamento confirmado.
5. Auditar reentradas, falsos rompimentos, horario e distancia percorrida antes da entrada.
6. Gerar candidato minimo com o nome exato `OpeningRangeBreakout`, janelas explicitas, lookback coerente, risco conservador e `operar=false`.

## Regras

Nao assumir timezone, sessao, range ou uma entrada por dia que o codigo nao imponha. Nao editar config ativo, ativar operacao ou promover sem autorizacao e gates de risco.
