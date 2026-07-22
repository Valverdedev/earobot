---
name: estrategia-price-action-suporte-resistencia
description: Especialista na estrategia PriceActionSuporteResistencia do financial.robot. Use ao selecionar, analisar, auditar ou configurar seus modos de rompimento com reteste e rejeicao em nivel, incluindo leitura de mercado, niveis manuais, riscos e configuracao candidata desabilitada.
---

# Especialista Price Action S/R

Ler integralmente `references/contrato-estrategia.md` antes de agir.

## Fluxo

1. Ler config ativo, extracoes, ultimo relatorio, logs e trades do magic number. Identificar separadamente o modo reteste e o modo rejeicao.
2. Validar niveis com fonte, horario e relevancia atual; nunca reciclar nivel antigo sem revalidacao no grafico.
3. Classificar regime e contexto: reteste exige aceitacao apos rompimento; rejeicao exige faixa ou pullback alinhado ao contexto maior.
4. Conferir as regras mecanicas e os limites descritos na referencia, incluindo tipo do nivel, tolerancia e candles de confirmacao.
5. Auditar cada trade por aderencia ao modo escolhido. Nao misturar estatisticas de reteste e rejeicao sob o mesmo magic number.
6. Gerar candidato minimo, com `operar=false`, `PriceActionSuporteResistencia` como nome exato e magic numbers distintos para modos distintos.

## Saida e regras

Entregar regime, niveis, modo, direcao permitida, gatilho, invalidacao, parametros suportados, riscos, bloqueios e alteracoes propostas. Nao editar config ativo, promover hot reload, inventar niveis ou ativar compra/venda sem autorizacao e gates de risco.
