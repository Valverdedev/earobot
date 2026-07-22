# Modelo obrigatorio do relatorio EURUSD

Use este esqueleto. Remova apenas linhas que sejam explicitamente opcionais; nunca remova fontes, riscos ou a proxima analise.

```markdown
# Analise EURUSD - <data/hora local ISO 8601>

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
- **Motivos objetivos:** <idade, evento, ATR, regime, fonte>
- **Itens mantidos:** <lista>
- **Itens atualizados/invalidados:** <lista>

## 3. Integridade dos dados do broker

| Verificacao | Resultado | Impacto |
|---|---:|---|
| Idade do tick | | |
| Idade dos candles | | |
| Ordem/duplicidade/gaps | | |
| Amostra M1/M5/M15/H1/H4/D1 | | |
| Posicoes abertas | | |
| Flags de risco | | |

## 4. Snapshot e metricas

| Metrica | Valor | Formula/origem |
|---|---:|---|
| Bid / Ask / Mid | | broker / calculo |
| Spread absoluto / pontos / pips | | |
| Spread / ATR M1 e M5 | | |
| ATR percentual por TF | | |
| Deslocamento desde analise anterior | | ATR H1 |

## 5. Fundamental e noticias

| Fato confirmado | Impacto provavel no EURUSD | Horizonte | Fonte direta | Publicado/evento em | Acessado em | Status |
|---|---|---|---|---|---|---|
| | | | | | | novo/mantido/invalidado |

### Agenda de risco

| Evento | Importancia | Horario local / UTC | Consenso | Realizado | Fonte |
|---|---|---|---:|---:|---|
| | | | | pendente | |

### Sintese fundamental

- **Vies altista para EURUSD:** <fatos e invalidacao>
- **Vies baixista para EURUSD:** <fatos e invalidacao>
- **Cenario-base:** <inferencia claramente rotulada>
- **Confianca:** <baixa/moderada/alta e por que>

## 6. Leitura tecnica top-down

| TF | Regime | Estrutura | EMA 9/21/50 | RSI 14 | ATR 14 | VWAP | Niveis/observacoes |
|---|---|---|---|---:|---:|---:|---|
| D1 | | | | | | | |
| H4 | | | | | | | |
| H1 | | | | | | | |
| M15 | | | | | | | |
| M5 | | | | | | | |
| M1 | | | | | | | |

### Price Action

- **Impulso/correcao:**
- **Rompimentos e follow-through:**
- **Pullbacks/retestes:**
- **Falhas/armadilhas:**
- **Qualidade dos candles:**
- **Suportes/resistencias e distancia em ATR:**

## 7. Lentes de leitura

### Al Brooks
<regime primeiro, always-in, canal/range, tentativas e follow-through>

### Stormer
<contexto, setup objetivo, gatilho, invalidacao, risco e repetibilidade>

### Fabricio Lorenz
<forca, impulso/correcao, nascimento/exaustao, pontos, rompimento/pullback>

### Consenso e divergencias
<o que converge, o que conflita e impacto na confianca>

## 8. Cenarios operacionais

| Cenario | Condicoes | Gatilho | Invalidacao | Objetivos tecnicos | Estrategia possivel | Validade |
|---|---|---|---|---|---|---|
| Altista EURUSD | | | | | | |
| Baixista EURUSD | | | | | | |
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
- **Modo e identidade da conta confirmados por:** <fonte ou NAO CONFIRMADOS>
- **Autorizacao reforcada para conta real:** <referencia ou nao aplicavel>
- **Config ativo alterado:** sim/nao
- **Backup:** <caminho ou nao aplicavel>

| Campo | Antes | Candidato | Justificativa |
|---|---|---|---|
| | | | |

## 11. Decisao final

<operar, aguardar gatilho ou nao operar; riscos e invalidacoes em linguagem direta>

## 12. Fontes

1. <titulo, instituicao, URL direta, publicado/evento em, acessado em>

## 13. Proxima analise obrigatoria

- **Data e hora:** <YYYY-MM-DDTHH:mm:ss-03:00> (<UTC>)
- **Motivo:** <evento, sessao ou limite de 4h>
- **Antecipar se:** movimento > 1 ATR H1; rompimento de <nivel>; spread > <limite>; dados stale; noticia material; mudanca de regime.
```

O ultimo conteudo do arquivo deve ser a secao `Proxima analise obrigatoria`. Nao acrescente observacoes depois dela.
