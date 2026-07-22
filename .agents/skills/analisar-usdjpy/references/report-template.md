# Modelo obrigatorio do relatorio USDJPY

Use este esqueleto. O ultimo conteudo do arquivo deve ser a secao `Proxima analise obrigatoria`.

```markdown
# Analise USDJPY - <data/hora local ISO 8601>

> Apoio a decisao em ambiente controlado. Nao constitui garantia de resultado.

## 1. Identificacao

| Campo | Valor |
|---|---|
| Gerado em | <local com fuso> / <UTC> |
| Terminal / simbolo | <terminal> / <simbolo> |
| Arquivo de extracao | <caminho> |
| Relatorio anterior | <caminho ou inexistente> |
| Config ativo lido | <caminho e hash/timestamp> |
| Qualidade geral | <aprovada, degradada ou bloqueada> |
| Decisao executiva | <estrategia ou NAO OPERAR> |

## 2. Decisao sobre a tese anterior

- **Acao:** <refazer integralmente / manter e revalidar>
- **Motivos objetivos:** <idade, evento, ATR, regime, fonte, sessao>
- **Itens mantidos:** <lista>
- **Itens atualizados/invalidados:** <lista>

## 3. Integridade dos dados do broker

| Verificacao | Resultado | Impacto |
|---|---:|---|
| Idade do tick | | |
| Idade dos candles | | |
| Ordem/duplicidade/gaps | | |
| Amostra M1/M5/M15/H1/H4/D1 | | |
| Metadados do simbolo | | |
| Sessao/feriado/rollover | | |
| Posicoes abertas | | |
| Flags de risco | | |

## 4. Snapshot e metricas

| Metrica | Valor | Formula/origem |
|---|---:|---|
| Bid / Ask / Mid | | broker / calculo |
| Spread absoluto / pontos | | |
| Spread / ATR M1 e M5 | | |
| ATR percentual por TF | | |
| Gap relevante | | |
| Deslocamento desde analise anterior | | ATR H1 |

## 5. Fundamental e noticias

| Fato confirmado | Impacto provavel no USDJPY | Horizonte | Fonte direta | Publicado/evento em | Acessado em | Status |
|---|---|---|---|---|---|---|
| | | | | | | novo/mantido/invalidado |

### Agenda de risco

| Evento | Importancia | Horario local / UTC | Consenso | Realizado | Fonte |
|---|---|---|---:|---:|---|
| | | | | pendente | |

### Sintese fundamental

- **Vies altista para USDJPY:**
- **Vies baixista para USDJPY:**
- **Cenario-base:**
- **Confianca:**

## 6. Leitura tecnica top-down

| TF | Regime | Estrutura | EMA 9/21/50 | RSI 14 | ATR 14 | VWAP | Niveis/observacoes |
|---|---|---|---|---:|---:|---:|---|
| D1 | | | | | | | |
| H4 | | | | | | | |
| H1 | | | | | | | |
| M15 | | | | | | | |
| M5 | | | | | | | |
| M1 | | | | | | | |

## 7. Lentes de leitura

### Al Brooks
### Stormer
### Fabricio Lorenz
### Consenso e divergencias

## 8. Cenarios operacionais

| Cenario | Condicoes | Gatilho | Invalidacao | Objetivos tecnicos | Estrategia possivel | Validade |
|---|---|---|---|---|---|---|
| Altista USDJPY | | | | | | |
| Baixista USDJPY | | | | | | |
| Neutro | | | | | nenhuma/range | |

## 9. Estrategia selecionada

- **Decisao:** <nome exato do catalogo ou NENHUMA>
- **Regime observado x regime exigido:**
- **Compatibilidade com o codigo atual:**
- **Limitacoes conhecidas:**
- **Por que as demais foram rejeitadas:**
- **Condicao de cancelamento:**

## 10. Config candidato e risco

- **Candidato:** <caminho>
- **Estado:** `operar=false` / promovido com autorizacao
- **Modo e identidade da conta confirmados por:** <fonte ou conta demo validada>
- **Autorizacao reforcada para conta real:** <referencia ou nao aplicavel>
- **Config ativo alterado:** sim/nao
- **Backup:** <caminho ou nao aplicavel>

| Campo | Antes | Candidato | Justificativa |
|---|---|---|---|
| | | | |

## 11. Decisao final

## 12. Fontes

1. <titulo, instituicao, URL direta, publicado/evento em, acessado em>

## 13. Proxima analise obrigatoria

- **Data e hora:** <YYYY-MM-DDTHH:mm:ss-03:00> (<UTC>)
- **Motivo:** <evento, sessao, rollover ou limite de 4h>
- **Antecipar se:** movimento > 1 ATR H1; rompimento de <nivel>; spread > <limite> sem filtro adequado; intervencao/verbal intervention; dados stale; noticia material; mudanca de regime.
```
