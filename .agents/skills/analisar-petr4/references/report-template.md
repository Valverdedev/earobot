# Modelo obrigatorio do relatorio PETR4

Usar este esqueleto. O ultimo conteudo deve ser `Proxima analise obrigatoria`.

```markdown
# Analise PETR4 - <data/hora ISO 8601>

> Apoio a decisao em ambiente controlado. Nao constitui garantia de resultado.

## 1. Identificacao

| Campo | Valor |
|---|---|
| Gerado em | <local / UTC> |
| Terminal / simbolo | `genial` / `PETR4` |
| Extracao | <caminho e timestamp> |
| Relatorio anterior | <caminho ou inexistente> |
| Config ativo | <caminho, hash, timestamp> |
| Qualidade geral | <aprovada/degradada/bloqueada> |
| Decisao executiva | <estrategia ou NAO OPERAR> |

## 2. Tese anterior e mudancas

- **Acao:** <refazer/manter>
- **Motivos:** <idade, evento, ATR, regime, fonte, sessao>
- **Mantido / atualizado / invalidado:**

## 3. Identidade e integridade

| Verificacao | PETR4 / genial | Impacto |
|---|---:|---|
| Conta/servidor/modo/titular | | |
| Bid / Ask / Mid | | |
| Point size / digits | | |
| Spread absoluto / pontos / ATR | | |
| Volume min/max/step | | |
| Tick e ultimo candle | | |
| Candles/gaps/duplicidades | | |
| Sessao/eventos corporativos | | |
| Posicoes abertas | | |

## 4. Tecnico top-down

| TF | Regime | Estrutura | EMA 9/21/50 | RSI 14 | ATR 14 | VWAP | Niveis |
|---|---|---|---|---:|---:|---:|---|
| D1 | | | | | | | |
| H4 | | | | | | | |
| H1 | | | | | | | |
| M15 | | | | | | | |
| M5 | | | | | | | |
| M1 | | | | | | | |

## 5. Matriz comportamental

| Fator | Estado | Canal | Confirma/contradiz | Horizonte | Fonte/timestamp | Confianca |
|---|---:|---|---|---|---|---|
| Petrobras RI/CVM | | | | | | |
| Brent/WTI/OPEP+ | | | | | | |
| Combustiveis/governo | | | | | | |
| USDBRL/DXY | | | | | | |
| DI/Selic/fiscal | | | | | | |
| Ibovespa/fluxo | | | | | | |
| PBR ADR/pares | | | | | | |

## 6. Eventos politicos e regulatorios

| Pessoa/orgao | Fato/fala | Canal economico | Reacao PETR4/BRL/DI | Fonte primaria | Publicado/acessado |
|---|---|---|---|---|---|
| | | | | | |

## 7. Agenda de risco

| Evento | Importancia | Horario local / UTC | Consenso | Realizado | Fonte |
|---|---|---|---:|---:|---|
| | | | | pendente | |

## 8. Lentes de leitura

### Al Brooks
<regime, always-in, canal/range, tentativas, follow-through>

### Stormer
<contexto, gatilho, invalidacao, risco, repetibilidade>

### Fabricio Lorenz
<forca, impulso/correcao, nascimento/exaustao, pontos e pullback>

## 9. Cenarios

| Cenario | Condicoes | Gatilho | Invalidacao | Objetivos | Estrategia/terminal | Validade |
|---|---|---|---|---|---|---|
| Altista | | | | | | |
| Baixista | | | | | | |
| Neutro | | | | | nenhuma/range | |

## 10. Estrategia e configs

- **Estrategia / terminal:** <catalogo ou NENHUMA>
- **Compatibilidade/limitacoes:**
- **Candidato PETR4:** <caminho, operar=true/false>
- **Config ativo alterado:** <sim/nao e motivo>
- **Backup/promocao:** <caminho ou nao aplicavel>

## 11. Decisao final

<operar, aguardar ou nao operar; fatos, inferencias, riscos e invalidacoes>

## 12. Fontes

1. <titulo, instituicao, URL direta, publicado/evento em, acessado em>

## 13. Proxima analise obrigatoria

- **Data e hora:** <YYYY-MM-DDTHH:mm:ss-03:00> (<UTC>)
- **Motivo:** <evento, sessao, limite de 4h ou fato relevante>
- **Antecipar se:** movimento > 1 ATR H1; ruptura; mudanca em Brent/USDBRL/DI; fato relevante; fala material; noticia de combustiveis/dividendos/governanca; spread/gap/stale.
```
