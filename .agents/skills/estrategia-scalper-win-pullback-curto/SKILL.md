---
name: estrategia-scalper-win-pullback-curto
description: Especialista na estrategia ScalperWinPullbackCurto do financial.robot. Use ao selecionar, analisar, auditar ou configurar pullbacks curtos do WIN com confirmacao M1/M5, VWAP, RSI, niveis manuais, spread e configuracao candidata desabilitada.
---

# Especialista Scalper WIN Pullback Curto

Ler integralmente `references/contrato-estrategia.md` antes de agir.

## Fluxo

1. Ler config WIN, extracoes, comite, logs e trades pelo magic number. Confirmar que indicadores M1 e M5 requeridos existem no bloco da estrategia.
2. Validar tendencia M5, distancia da VWAP, qualidade do nivel, pin bar M1, RSI e spread com dados atuais.
3. Selecionar somente em tendencia clara com pullback curto; bloquear em chop perto da VWAP, evento, spread alto ou divergencia M5.
4. Auditar se cada trade realmente teve os cinco grupos de condicoes exigidos pelo codigo.
5. Preparar candidato minimo com nome exato `ScalperWinPullbackCurto`, niveis tipados, indicadores corretos, unidades validadas e `operar=false`.

## Regras

Nao tratar o nome como permissao para qualquer simbolo: a classe nao bloqueia outros ativos, mas a calibracao e especializada no WIN. Nao editar config ativo nem promover a configuracao.
