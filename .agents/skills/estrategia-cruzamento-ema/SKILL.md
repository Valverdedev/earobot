---
name: estrategia-cruzamento-ema
description: Especialista na estrategia CruzamentoEma do financial.robot. Use ao selecionar, analisar, auditar ou configurar CruzamentoEma, incluindo leitura de mercado e relatorios, validacao de cenario, riscos de whipsaw, indicadores EMA/RSI/ForcaCesta e criacao de configuracao candidata desabilitada.
---

# Especialista Cruzamento EMA

Ler integralmente `references/contrato-estrategia.md` antes de analisar ou configurar.

## Fluxo

1. Ler o config ativo do simbolo, extracoes de mercado, ultimo relatorio e logs/trades relevantes. Para WIN, complementar com os relatorios do comite e as skills de contexto, fluxo, price action, regime e risco.
2. Confirmar contrato, timestamp, timeframe, bid/ask, tamanho do ponto, spread, posicoes e limites. Separar fatos, calculos e inferencias.
3. Validar o regime contra o contrato do codigo: CruzamentoEma e para transicao ou retomada de tendencia, nao para uma tendencia ja esticada ou lateralidade.
4. Conferir cada pre-requisito da referencia. Se EMA, RSI ou ForcaCesta nao puderem ser calculados como o codigo espera, recomendar `NAO OPERAR`.
5. Auditar a execucao por magic number sem avaliar apenas PnL. Um trade vencedor fora das regras ainda e falha de disciplina.
6. Quando selecionada, gerar um config candidato a partir do ativo: preservar campos desconhecidos, magic number e limites; usar o nome exato `CruzamentoEma`; alterar somente parametros suportados; definir `operar=false`.

## Saida

Entregar um relatorio auditavel e, quando houver evidencia suficiente, um candidato JSON. Incluir regime, direcao permitida, gatilho, invalidacao, indicadores requeridos, riscos, bloqueios, alteracoes propostas e justificativa de cada alteracao.

## Regras

- Reler a fonte de codigo indicada na referencia se ela tiver mudado desde a ultima analise.
- Nao inventar candles, indicadores, custos, resultados ou configuracoes eficazes.
- Nao editar config ativo, ativar estrategia, aumentar lote ou promover hot reload. Promocao requer autorizacao explicita e a skill de promocao segura.
- Se o mercado estiver lateral, os dados forem insuficientes ou a unidade de preco for incerta, produzir candidato desabilitado ou recomendar nao selecionar a estrategia.
