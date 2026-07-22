---
name: estrategia-microtendencia-pullback-ema
description: Especialista na estrategia MicroTendenciaPullbackEma do financial.robot. Use ao selecionar, analisar, auditar ou configurar microtendencias com tres EMAs, ATR, RSI, VWAP, spread e pullback de continuacao, incluindo configuracao candidata desabilitada.
---

# Especialista Microtendencia Pullback EMA

Ler integralmente `references/contrato-estrategia.md` antes de agir.

## Fluxo

1. Ler config, extracoes, relatorios, logs e trades. Validar historico, spread, EMAs, ATR, RSI e VWAP quando o filtro estiver ativo.
2. Confirmar alinhamento e inclinacao das tres EMAs, preco do lado correto da EMA lenta, pullback dentro da zona e candle de retomada.
3. Selecionar em dia direcional com recuos ordenados; bloquear em medias emboladas, ATR baixo, candle de noticia e rompimento ja exaurido.
4. Auditar cada trade contra todos os filtros mecanicos, inclusive os que sao opcionais na configuracao.
5. Gerar candidato minimo com nome `MicroTendenciaPullbackEma`, parametros existentes, unidades de preco validadas e `operar=false`.

## Regras

Nao reduzir riscos apenas para fazer caber um candle grande e nao usar RSI isoladamente como direcao. Nao editar config ativo ou promover hot reload.
