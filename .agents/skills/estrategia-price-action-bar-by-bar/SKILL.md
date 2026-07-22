---
name: estrategia-price-action-bar-by-bar
description: Especialista na estrategia PriceActionBarByBar do financial.robot. Use ao selecionar, analisar, auditar ou configurar impulso, pullback e rompimento de barra de sinal, incluindo leitura de candles, filtro de climax, stop estrutural, relatorios e configuracao candidata desabilitada.
---

# Especialista Price Action BarByBar

Ler integralmente `references/contrato-estrategia.md` antes de agir.

## Fluxo

1. Ler config, extracoes graficas, ultimo relatorio, logs e ordens pelo magic number. Reproduzir visualmente cada sinal com candles fechados.
2. Classificar tendencia, range, impulso, pullback e exaustao. Selecionar somente quando houver direcao clara e estrutura limpa.
3. Conferir corpo/range, tamanho do impulso e pullback, filtro de climax, fechamento de confirmacao e stop estrutural conforme a referencia.
4. Validar risco real entre entrada e stop sugerido, unidade de preco, minimo de stop da corretora e TP resultante; bloquear se a distancia for anomala ou inviavel.
5. Auditar trades em sequencia, entradas contra vies maior, operacoes apos climax e qualquer divergencia entre log, grafico e ordem.
6. Gerar candidato minimo com nome `PriceActionBarByBar`, parametros existentes, direcao controlada e `operar=false`.

## Regras

Nao transformar leitura discrecionaria em regra que o codigo nao possui. A estrategia nao filtra timeframe maior, spread, horario ou volume por si; exigir esses gates externamente. Nao editar config ativo ou promover a configuracao.
