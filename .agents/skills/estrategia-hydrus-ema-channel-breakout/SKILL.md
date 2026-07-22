---
name: estrategia-hydrus-ema-channel-breakout
description: Especialista na estrategia HydrusEmaChannelBreakout do financial.robot. Use ao selecionar, analisar, auditar ou configurar rompimentos do canal em torno de EMA longa, incluindo calibracao de canal, confirmacao de timeframe maior, risco de volatilidade e configuracao candidata desabilitada.
---

# Especialista Hydrus EMA Channel

Ler integralmente `references/contrato-estrategia.md` antes de agir.

## Fluxo

1. Ler config, extracoes, relatorios, logs e trades. Confirmar o timeframe-base e se os candles fornecidos sao M1 quando a confirmacao maior estiver ligada.
2. Medir volatilidade e estabilidade do canal antes de usar `canalPontos`; um canal fixo nao se adapta sozinho ao regime.
3. Validar cruzamento real da media curta contra a banda, spread, distancia apos rompimento e confirmacao de timeframe maior, quando habilitada.
4. Bloquear entradas perseguidas, mercado lateral, canal obsoleto e dados insuficientes para a EMA longa ou confirmacao.
5. Auditar cruzamentos, distancia de entrada, sentido permitido e comportamento em mudanca de volatilidade.
6. Gerar candidato minimo com nome `HydrusEmaChannelBreakout`, apenas parametros existentes e `operar=false`.

## Regras

Nao supor ATR, volume, fechamento fora da banda ou stop estrutural: esses filtros nao pertencem a esta classe. Nao editar config ativo nem promover configuracao.
