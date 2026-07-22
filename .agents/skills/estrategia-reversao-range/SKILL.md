---
name: estrategia-reversao-range
description: Especialista na estrategia ReversaoRange do financial.robot. Use ao selecionar, analisar, auditar ou configurar reversao a media em suportes e resistencias, incluindo leitura de regime lateral, RSI, riscos de rompimento e configuracao candidata desabilitada.
---

# Especialista Reversao de Range

Ler integralmente `references/contrato-estrategia.md` antes de agir.

## Fluxo

1. Ler config, extracoes, relatorios, logs e trades. Mapear suportes e resistencias com evidencia grafica atual.
2. Classificar o mercado como range maduro, transicao ou tendencia. Selecionar a estrategia somente no primeiro caso.
3. Conferir RSI, proximidade do nivel, spread, amplitude do range, evento e sinais de aceitacao fora da faixa.
4. Aplicar literalmente as condicoes do codigo e registrar os filtros que o codigo nao possui.
5. Auditar persistencia de RSI extremo, repeticao de entradas e trades contra rompimento real.
6. Gerar candidato minimo com nome `ReversaoRange`, niveis tipados, limites de RSI e distancia em unidade validada; manter `operar=false`.

## Regras

RSI extremo nao e confirmacao de reversao. Nao inventar rejeicao de candle, filtro de tendencia ou alvo automatico que o codigo nao implemente. Nao promover ou editar config ativo.
