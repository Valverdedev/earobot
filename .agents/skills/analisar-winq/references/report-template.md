# Modelo obrigatorio do relatorio WINQ/Bra50

Usar este esqueleto. O ultimo conteudo deve ser `Proxima analise obrigatoria`.

```markdown
# Analise WINQ/Bra50 - <data/hora ISO 8601>

> Apoio a decisao em ambiente controlado. Nao constitui garantia de resultado.

## 1. Identificacao

| Campo | Valor |
|---|---|
| Gerado em | <local / UTC> |
| Genial / ActivTrades | `WINQ26` / `Bra50Aug26` |
| Extracoes | <dois caminhos e timestamps> |
| Relatorio anterior | <caminho ou inexistente> |
| Configs ativos | <caminhos, hashes, timestamps> |
| Qualidade geral | <aprovada/degradada/bloqueada> |
| Decisao executiva | <estrategia ou NAO OPERAR> |

## 2. Tese anterior e mudancas

- **Acao:** <refazer/manter>
- **Motivos:** <idade, evento, ATR, regime, fonte, sessao>
- **Mantido / atualizado / invalidado:**

## 3. Identidade e integridade

| Verificacao | WINQ26 / genial | Bra50Aug26 / activtraders | Impacto |
|---|---:|---:|---|
| Conta/servidor/modo/titular | | | |
| Bid / Ask / Mid | | | |
| Point size / digits | | | |
| Spread absoluto / pontos / ATR | | | |
| Volume min/max/step | | | |
| Tick e ultimo candle | | | |
| Candles/gaps/duplicidades | | | |
| Sessao/contrato/rollover | | | |
| Posicoes abertas | | | |

### Paridade entre corretoras

| Janela | Retorno WINQ26 | Retorno Bra50Aug26 | Divergencia | Correlacao/observacao |
|---|---:|---:|---:|---|
| | | | | |

- **Escala/basis:** <validada/formula ou NAO CALCULADO>
- **Exposicao duplicada:** <resultado>

## 4. Tecnico top-down

| Simbolo | TF | Regime | Estrutura | EMA 9/21/50 | RSI 14 | ATR 14 | VWAP | Niveis |
|---|---|---|---|---|---:|---:|---:|---|
| WINQ26 | D1/H4/H1/M15/M5/M1 | | | | | | | |
| Bra50Aug26 | D1/H4/H1/M15/M5/M1 | | | | | | | |

### Price Action consolidado

- **Impulso/correcao:**
- **Rompimento/pullback/falha:**
- **Abertura, ajuste e gaps:**

## 5. Matriz comportamental

| Fator | Estado | Canal | Confirma/contradiz | Horizonte | Fonte/timestamp | Confianca |
|---|---:|---|---|---|---|---|
| USDBRL/WDO/DXY | | | | | | |
| DI/Selic/fiscal | | | | | | |
| Vale/minerio/China | | | | | | |
| Petrobras/Brent | | | | | | |
| Bancos | | | | | | |
| S&P/Nasdaq/VIX/UST | | | | | | |
| Fluxo/portfolio B3 | | | | | | |

### Cesta de acoes

| Acao | Peso oficial | Retorno | Contribuicao aproximada | Fonte |
|---|---:|---:|---:|---|
| | | | | |

- **ForcaCesta do robo:** <componentes/pesos/score/timestamp>
- **Diferenca para carteira oficial:**

## 6. Eleicao 2026

### Pesquisas registradas

| PesqEle | Instituto/contratante | Campo/metodo/amostra | Resultado | Comparacao valida | Impacto observado | Fonte/acesso |
|---|---|---|---|---|---|---|
| | | | | | | |

### Falas e propostas

| Pessoa | Status | Fato/fala | Canal economico | Reacao BRL/DI/WIN | Fonte primaria | Publicado/acessado |
|---|---|---|---|---|---|---|
| Lula | | | | | | |
| Flavio Bolsonaro | | | | | | |
| Outro nome material | | | | | | |

- **Inferencia eleitoral / confianca / invalidacao:**

## 7. Tarifas e comercio exterior

| Fase | Medida/escopo | Setores/empresas | Cambio/inflacao/fiscal | Status/prazo | Fonte/acesso |
|---|---|---|---|---|---|
| | | | | | |

- **Impacto direto / indireto / dispersao setorial:**
- **Proximo gatilho formal:**

## 8. Agenda de risco

| Evento | Importancia | Horario local / UTC | Consenso | Realizado | Fonte |
|---|---|---|---:|---:|---|
| | | | | pendente | |

## 9. Lentes de leitura

### Al Brooks
<regime, always-in, canal/range, tentativas, follow-through>

### Stormer
<contexto, gatilho, invalidacao, risco, repetibilidade>

### Fabricio Lorenz
<forca, impulso/correcao, nascimento/exaustao, pontos e pullback>

### Consenso e divergencias
<tecnico x intermercado x eventos>

## 10. Cenarios

| Cenario | Condicoes | Gatilho | Invalidacao | Objetivos | Estrategia/terminal | Validade |
|---|---|---|---|---|---|---|
| Altista | | | | | | |
| Baixista | | | | | | |
| Neutro | | | | | nenhuma/range | |

## 11. Estrategia e configs

- **Estrategia / terminal preferido:** <catalogo ou NENHUMA>
- **Compatibilidade/limitacoes:**
- **Candidato WINQ26:** <caminho, operar=false>
- **Candidato Bra50Aug26:** <caminho, operar=false>
- **Configs ativos alterados:** <sim/nao e motivo>
- **Conta real/segunda confirmacao:** <referencia ou nao aplicavel>
- **Backup/promocao:** <caminho ou nao aplicavel>

## 12. Decisao final

<operar, aguardar ou nao operar; fatos, inferencias, riscos e invalidacoes>

## 13. Fontes

1. <titulo, instituicao, URL direta, publicado/evento em, acessado em>

## 14. Proxima analise obrigatoria

- **Data e hora:** <YYYY-MM-DDTHH:mm:ss-03:00> (<UTC>)
- **Motivo:** <evento, sessao, limite de 4h ou rollover>
- **Antecipar se:** movimento > 1 ATR H1; ruptura; mudanca em USDBRL/DI; nova PesqEle; fala material; decisao tarifaria; divergencia WINQ/Bra50; spread/gap/stale; rollover ou noticia material.
```
