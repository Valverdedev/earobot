---
name: analisar-petr4
description: Analise profissional, comportamental e auditavel de PETR4/Petrobras PN para este robo operar no terminal Genial. Use ao pesquisar fatores que influenciam PETR4, incluindo Petrobras, Brent, diesel/gasolina, governanca estatal, dividendos, Ibovespa, dolar, juros, politica fiscal, eleicao, noticias e Price Action, selecionar estrategia registrada em docs/catalogo-estrategias.md, preparar relatorio e config candidato, e promover o config para D:\SistemEarobot\config quando autorizado.
---

# Analisar PETR4

## Objetivo

Produzir uma leitura operacional rastreavel de `PETR4` no terminal `genial`, combinando dados reais do MetaTrader 5, Price Action, contexto Petrobras/petroleo, macro Brasil e risco politico/regulatorio.

Admitir `NAO OPERAR` quando mercado, dados, eventos, liquidez, ticker, lote, custos ou estrategia nao estiverem validados. Nao tratar a analise como garantia de retorno.

Ler integralmente antes de agir:

- `references/petr4-market-framework.md`
- `references/report-template.md`
- `D:\SistemEarobot\financial.robot\docs\catalogo-estrategias.md`

## Instrumentos e caminhos

| Papel | Terminal | Simbolo | Config ativo |
|---|---|---|---|
| Petrobras PN B3 | `genial` | `PETR4` | `D:\SistemEarobot\config\PETR4.config.json` |

- Relatorios: `D:\SistemEarobot\relatorios\PETR4`
- Preparador: `scripts/Prepare-Petr4Analysis.ps1`
- Extrator: `D:\SistemEarobot\financial.robot\publish-extractor\Financial.Robot.Extractor.exe`
- Catalogo: `D:\SistemEarobot\financial.robot\docs\catalogo-estrategias.md`

## Regras inegociaveis

1. Nao inventar cotacoes, candles, proventos, noticias, consenso, realizado, pesos, niveis ou metadados do broker.
2. Marcar dado nao confirmado como `[NAO CONFIRMADO]` e nao usa-lo como premissa decisiva.
3. Registrar URL direta, publicado/evento em e acessado em para toda afirmacao material externa.
4. Separar `fato`, `calculo`, `inferencia` e `cenario`.
5. Usar horario ISO 8601 com fuso `America/Sao_Paulo`; incluir UTC em eventos de alto impacto.
6. Conferir se `PETR4` e negociavel no terminal `genial`, com bid/ask, tick, volume minimo e sessao validos.
7. Tratar Petrobras como estatal com risco de governanca, combustiveis, dividendos, capex e politica publica; nao inferir impacto somente pelo Brent.
8. Monitorar Brent/WTI, combustiveis, USDBRL, Ibovespa, fluxo estrangeiro, DI/Selic, fiscal e noticias de governo.
9. Nao transformar fala isolada em sinal. Classificar canal: combustiveis, dividendos, capex, governanca, impostos, fiscal, Petrobras, energia ou comercio exterior.
10. Nao forcar estrategia. `Nenhuma estrategia / operar=false` e valido, exceto quando a execucao atual autorizar explicitamente operar.
11. Nao aumentar lote, risco, drawdown, posicoes ou metas sem autorizacao explicita da execucao atual.
12. Se o usuario autorizar nesta execucao, pode promover `operar=true`, `comprar=true`, `vender=true` apos validar dados, JSON e ausencia de bloqueio operacional.

## Fluxo obrigatorio

### 1. Preparar e extrair

Executar:

```powershell
& "D:\SistemEarobot\financial.robot\.agents\skills\analisar-petr4\scripts\Prepare-Petr4Analysis.ps1" -RunExtractor
```

Capturar o objeto retornado. Se a extracao falhar, registrar diagnostico, criar relatorio degradado e nao promover config ativo.

### 2. Ler estado anterior

Ler integralmente o ultimo relatorio `yyyy-MM-dd_HH-mm-ss_petr4.md`, o JSON extraido, o config ativo quando existir, o catalogo, posicoes abertas e alertas de sessao/corporativos. Registrar tese anterior, invalidacoes, ATR/regime, eventos, hashes e exposicao agregada.

### 3. Validar o instrumento

Conferir terminal, conta, modo, titular, bid/ask, point size, digits, stops level, volume, candle, sessao, posicoes, lote minimo e negociabilidade.

### 4. Pesquisar contexto

Pesquisar fontes atuais e avaliar no minimo:

- Petrobras RI/CVM: fatos relevantes, comunicados, resultados, proventos, guidance, capex, producao e politica de dividendos;
- Brent/WTI, OPEP+, geopolitica e estoques EIA/API quando materiais;
- politica de precos de combustiveis, diesel/gasolina/GLP e falas de governo;
- USDBRL, DXY, DI/Selic, fiscal, inflacao e fluxo para Brasil;
- Ibovespa, PETR3, pares globais de petroleo e ADR PBR;
- agenda B3, Petrobras, macro Brasil/EUA e eventos de alta volatilidade;
- eleicao, estatais e regulacao quando material.

Usar fontes primarias primeiro. Reuters/AP podem apoiar tempo real. Post social so vale como fonte primaria da fala quando a conta for confirmada; corroborar consequencias em fonte independente.

### 5. Atualizar ou manter a tese

Refazer integralmente quando nao houver relatorio, a tese tiver mais de 6 horas, houver fato relevante, noticia material sobre combustiveis/dividendos/governanca, dado macro, movimento acima de 1 ATR H1, mudanca de regime, abertura de sessao ou conflito de fontes.

Manter e revalidar somente quando tiver ate 2 horas e nenhum gatilho tiver ocorrido. Entre 2 e 6 horas, justificar conservadoramente. Rotular `mantido`, `atualizado` ou `invalidado`.

### 6. Ler grafico e intermercado

Fazer leitura `D1 -> H4 -> H1 -> M15 -> M5 -> M1`. Calcular metricas do framework e classificar tendencia, range, transicao, impulso, correcao, compressao, expansao, rompimento, reteste e falha.

Construir tabela de confirmacoes/contradicoes entre PETR4, PETR3, PBR ADR, Brent, USDBRL, Ibovespa, DI e exterior.

Aplicar separadamente as lentes publicas de Al Brooks, Alexandre Wolwacz (Stormer) e Fabricio Lorenz. Nao imitar a voz deles nem atribuir ordem especifica.

### 7. Montar cenarios

Produzir cenarios altista, baixista e neutro com condicoes, gatilho, invalidacao, objetivos, risco, validade e estrategia candidata ou `nenhuma`.

Se houver evento de alto impacto nos proximos 30 minutos, fato relevante recem-divulgado sem absorcao de preco, fala ao vivo relevante, spread anormal ou stale data, manter candidatos com `operar=false` e reavaliar apos normalizacao.

### 8. Selecionar estrategia

Releia o catalogo a cada execucao. Escolher somente estrategia existente e compativel. Para PETR4, preferir setups que tolerem acao liquida de B3 e custos: `CruzamentoEma` em M5/M15 quando houver tendencia e `PriceActionSuporteResistencia` para niveis tecnicos relevantes. Evitar `ScalperWinPullbackCurto` por ser especializado em WIN e ter bloqueio de lookback catalogado.

### 9. Preparar candidato e promover quando autorizado

Partir do config ativo quando existir e alterar o minimo. Se nao existir, criar config novo com magic numbers exclusivos de PETR4. Preservar campos desconhecidos e limites.

Por padrao salvar candidato com `operar=false`. Quando a mensagem atual do usuario autorizar operar/promover, pode salvar e promover com `operar=true`, `comprar=true`, `vender=true`, desde que todos os gates estejam aprovados:

1. conta, servidor, titular e modo confirmados por fonte do extrator/config;
2. JSON, schema, parametros, estrategia e unidade validados;
3. sem posicao/conflito que torne hot reload inseguro;
4. sem evento iminente, stale data, divergencia material ou bloqueio de qualidade;
5. risco, lote, stops, spread e alvo coerentes com PETR4.

Antes de promover, criar backup timestampado e substituir atomicamente.

### 10. Finalizar e verificar

Usar `references/report-template.md`. O ultimo bloco deve ser `Proxima analise obrigatoria`, com horario absoluto e gatilhos antecipados.

Confirmar nomes, fontes/horarios, calculos, candidatos JSON validos, config ativo promovido somente se autorizado e gates aprovados, e proxima analise definida.
