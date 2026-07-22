---
name: analisar-usdjpy
description: Analise profissional e auditavel de USDJPY para este robo, combinando Price Action, indicadores, dados do broker, noticias e fundamentos com fontes. Use ao gerar ou atualizar relatorios de USD/JPY, decidir entre manter ou refazer a tese fundamental, selecionar uma estrategia registrada em docs/catalogo-estrategias.md, preparar um config candidato e, sob autorizacao e controles de seguranca, promover o config para D:\SistemEarobot\config.
---

# Analisar USDJPY

## Objetivo

Produza uma decisao operacional rastreavel para o simbolo USDJPY do broker. Trate a saida como apoio a decisao, nunca como garantia de retorno. Admita `NAO OPERAR` quando dados, contexto, liquidez, risco de evento, intervencao cambial, spread, sessao ou compatibilidade do codigo forem insuficientes.

Use as lentes publicamente documentadas de Al Brooks, Alexandre Wolwacz (Stormer) e Fabricio Lorenz. Nao imite a voz dos especialistas, nao atribua a eles uma recomendacao especifica e nao invente regras proprietarias.

Leia antes de iniciar:

- `references/usdjpy-market-framework.md`
- `references/report-template.md`
- `D:\SistemEarobot\financial.robot\docs\catalogo-estrategias.md`

## Caminhos fixos

- Relatorios: `D:\SistemEarobot\relatorios\USDJPY`
- Prompt operacional: `D:\SistemEarobot\relatorios\USDJPY\prompt_analise_usdjpy.md`
- Extrator: `D:\SistemEarobot\financial.robot\publish-extractor\Financial.Robot.Extractor.exe`
- Catalogo: `D:\SistemEarobot\financial.robot\docs\catalogo-estrategias.md`
- Config ativo: `D:\SistemEarobot\config\USDJPY.config.json`
- Terminal padrao: `activtraders`
- Simbolo padrao do broker: `USDJPY`

## Conta validada

- A conta `6252382`, servidor `ActivTradesCorp-Server`, titular `Felipe Valverde`/`FELIPE VALVERDE`, esta validada pelo usuario FELIPE VALVERDE como conta demonstracao. Para esta conta especifica, a identidade e o modo demo podem ser tratados como confirmados quando o extrator retornar o mesmo login, servidor e titular.
- A validacao acima nao se estende a outro login, servidor ou titular. Se qualquer um desses campos divergir, volte a exigir confirmacao independente do modo e da identidade da conta antes de promover `operar=true`.
- Por ser demo validada, nao se aplica a exigencia de segunda confirmacao de risco de ordens reais enquanto login, servidor e titular coincidirem exatamente com a conta validada.

## Regras inegociaveis

1. Nao invente cotacoes, candles, noticias, agenda, consenso, realizado, falas, posicionamento, niveis ou fundamentos.
2. Marque qualquer dado nao confirmado como `[NAO CONFIRMADO]` e nao o use como premissa decisiva.
3. Registre URL direta, horario do evento/publicacao e horario de acesso em toda afirmacao fundamental material.
4. Diferencie fato, calculo, inferencia e cenario. Explicite as formulas dos calculos relevantes.
5. Use horario ISO 8601 com fuso, preferencialmente `America/Sao_Paulo`, e informe tambem UTC nos eventos de alto impacto.
6. Nao presuma pip, point size, tick value, volume, stop level ou horario; use metadados extraidos do `USDJPY` no broker.
7. Trate risco de intervencao verbal/real do Japao como risco de gap/slippage quando USDJPY estiver esticado, em nivel psicologico ou apos comunicados de MoF/BoJ.
8. Nao force uma estrategia. `Nenhuma estrategia / operar=false` e uma decisao valida.
9. Nao aumente lote, risco, drawdown, numero de posicoes, metas globais ou permissao de operar sem autorizacao explicita.
10. Nao altere o config ativo apenas para concluir a tarefa. A gravacao nele pode acionar hot reload e ordens.
11. Nao alegue rentabilidade, robustez estatistica ou adequacao ao vivo sem backtest, custos realistas, teste fora da amostra e verificacao operacional.

## Fluxo obrigatorio

### 1. Preparar a execucao

Execute:

```powershell
& "D:\SistemEarobot\financial.robot\.agents\skills\analisar-usdjpy\scripts\Prepare-USDJPYAnalysis.ps1" -RunExtractor
```

O script localiza o relatorio anterior por nome exato, prepara os nomes da nova execucao e extrai `M1,M5,M15,H1,H4,D1` com 500 candles. Capture o objeto retornado. O relatorio deve se chamar `yyyy-MM-dd_HH-mm-ss_usdjpy.md`; nao use dois-pontos no nome.

Se o extrator falhar, pare a recomendacao operacional. Documente o erro e produza somente diagnostico, sem config ativo.

### 2. Ler o estado anterior

Leia integralmente o relatorio anterior, quando existir, o JSON extraido e o config ativo. Nao trate prompt, extracoes JSON ou configs candidatos como relatorios anteriores.

Registre caminho/timestamp do relatorio anterior, tese anterior e invalidacoes, estrategia/config anteriores, eventos previstos desde a analise anterior e diferencas observadas no mercado/dados.

### 3. Decidir sobre a analise fundamental

Atualize sempre cotacao do broker, qualidade dos dados, agenda e noticias. Refaca a tese fundamental completa quando ocorrer qualquer item:

- nao existe relatorio anterior;
- analise anterior tem mais de 6 horas;
- houve FOMC, ata, discurso material do Fed, CPI, PPI, PCE, payroll, jobless claims, ISM/PMI, retail sales, GDP, leilao do Treasury ou dado de inflacao/atividade/emprego do Japao;
- houve decisao, discurso ou operacao do BoJ, MoF, FSA ou comunicacao sobre cambio/intervencao;
- houve movimento material em US yields, JGB yields, diferencial 2y/10y EUA-Japao, DXY, Nikkei, VIX ou apetite por risco;
- o preco se deslocou pelo menos 1 ATR de H1 desde a referencia;
- mudou o regime tecnico de H1/H4 ou houve rompimento de nivel de invalidacao;
- iniciou sessao relevante: Tokyo, Londres, New York, fixings, rolagem diaria, nova semana ou reabertura apos fim de semana/feriado;
- mudou a liquidez por feriado EUA/Japao, rollover, horario de verao/inverno ou evento de fim de dia;
- as fontes anteriores estao incompletas, conflitantes ou indisponiveis.

Mantenha e revalide a tese somente quando ela tiver no maximo 2 horas, nenhum gatilho acima tiver ocorrido e as fontes continuarem atuais. Mesmo nesse caso, pesquise o que mudou e rotule cada item como `mantido`, `atualizado` ou `invalidado`. Entre 2 e 6 horas, use julgamento conservador e justifique.

### 4. Validar os dados do broker

Antes da leitura grafica, confira:

- idade do tick e do ultimo candle;
- ordem temporal, duplicidades e gaps, especialmente em Tokyo open, London open, NY data, NY open, rollover e reabertura semanal;
- quantidade de candles por timeframe;
- bid, ask, point size, digits, spread, stops level, min/max volume e volume step;
- posicoes abertas, margem livre e eventuais flags de risco;
- continuidade suficiente para EMA, RSI, ATR e VWAP;
- se spread, stop minimo e distancia de stop/target sao compativeis com o ATR do timeframe escolhido;
- se `USDJPY` esta em periodo normal de negociacao, sem feriado, rollover ou simbolo obsoleto.

Se houver dados stale, spread invalido, gaps materiais, rolagem problematica ou amostra insuficiente, reduza a confianca. Bloqueie novas entradas quando a falha puder mudar a direcao, os niveis ou o dimensionamento.

Spread alto, sozinho, nao deve ser usado como motivo para alterar o config global para `operar=false` quando ja existir filtro operacional de spread minimo/maximo capaz de bloquear a entrada no momento da execucao. Nesse caso, documente o spread, confirme que o filtro existe e esta coerente com ATR/stop, e deixe que o motor bloqueie a ordem se o spread continuar inadequado. So use spread como bloqueio de promocao ou motivo para `operar=false` quando ele estiver invalido, anormal de forma persistente, sem filtro operacional adequado, ou capaz de distorcer stop, alvo, nivel ou dimensionamento.

### 5. Pesquisar o contexto fundamental

Pesquise fontes atuais na internet. Priorize fontes primarias: Federal Reserve, BLS, BEA, U.S. Treasury, Bank of Japan, Ministry of Finance Japan, Cabinet Office Japan, Statistics Bureau of Japan, FRED, CME e comunicados oficiais. Use Reuters ou AP para noticia em tempo real e geopolitica, preferencialmente com corroboracao.

Avalie no minimo:

- Fed, BoJ, diferencial de juros, US Treasury yields, JGB yields e curva;
- inflacao, emprego, consumo e atividade dos EUA e Japao;
- DXY, Nikkei, VIX, risco global e carry trade;
- risco de intervencao MoF/BoJ, niveis psicologicos e comentarios oficiais;
- calendario macro de alto impacto e janela ate o proximo evento;
- sessoes Tokyo/London/NY, rollover diario, feriados e liquidez.

### 6. Calcular e ler o grafico

Calcule as metricas de `references/usdjpy-market-framework.md` a partir do JSON. Faca leitura top-down nesta ordem: `D1 -> H4 -> H1 -> M15 -> M5 -> M1`.

Classifique em cada timeframe: tendencia, range amplo, range estreito, transicao ou dados inconclusivos; estrutura de topos/fundos; impulso/correcao; compressao/expansao; rompimento, continuidade, pullback/reteste e falha; suportes/resistencias/VWAP; qualidade das barras; alinhamento ou divergencia entre EMA 9/21/50, RSI 14, ATR 14 e VWAP.

### 7. Aplicar as tres lentes

Apresente as lentes separadamente e depois consolide consenso e divergencias:

- **Brooks:** tendencia versus trading range; canal, tight/broad range, always-in, follow-through, pullbacks, segundas tentativas e rompimentos falhos.
- **Stormer:** contexto, setup objetivo, gatilho, invalidacao, risco e repetibilidade.
- **Lorenz:** forca dos candles, impulso/correcao, nascimento/exaustao, pontos relevantes, linhas de tendencia, rompimento/pullback.

Nao declare que um especialista `compraria` ou `venderia`. Declare apenas o que a lente metodologica sugere no conjunto de dados observado.

### 8. Montar cenarios

Produza cenarios altista para USDJPY, baixista para USDJPY e neutro. Para cada um, informe condicoes necessarias, gatilho observavel, invalidacao, objetivos tecnicos, riscos de evento/spread/intervencao/sessao, timeframe de validade e estrategia candidata ou `nenhuma`.

Se houver evento de alto impacto nos proximos 30 minutos, mantenha o candidato com `operar=false`, salvo regra operacional mais restritiva ja existente. Reavalie 15 minutos depois da divulgacao ou somente quando spread, liquidez e estrutura normalizarem.

### 9. Selecionar uma estrategia implementada

Releia o catalogo a cada execucao. Escolha somente estrategia existente no codigo e compare o regime exigido com o regime observado. Prefira uma unica estrategia ativa para evitar sobreposicao de sinais e risco.

Respeite as restricoes catalogadas:

- nao selecione `ScalperWinPullbackCurto` enquanto a retencao do motor nao suportar os 105 candles exigidos;
- nao use `PriceActionSuporteResistencia` em modo reteste enquanto a conversao booleana documentada nao for corrigida e testada;
- trate `OpeningRangeBreakout` como agressiva e sensivel a reinicio/historico; em USDJPY use apenas com janela de sessao/fixing bem definida;
- calibre canais e niveis fixos de `HydrusEmaChannelBreakout` com dados atuais do `USDJPY`;
- use `ReversaoRange` apenas em range estabelecido e fora de janela de noticia/intervencao;
- use `CruzamentoEma` somente em inicio/retomada de tendencia com confirmacao, evitando lateralidade.

### 10. Preparar o config candidato

Parta do config ativo e altere o minimo necessario. Preserve campos desconhecidos, magic numbers, limites de risco e configuracoes globais. Salve o candidato no caminho indicado pelo script, dentro da pasta de relatorios, com `operar=false` por padrao.

Promova para `D:\SistemEarobot\config\USDJPY.config.json` somente quando todas as condicoes forem verdadeiras:

1. O usuario autorizou explicitamente a ativacao nesta execucao.
2. Uma fonte independente e autoritativa confirmou modo e identidade da conta; para a conta demo validada `6252382` / `ActivTradesCorp-Server` / `Felipe Valverde`, a confirmacao do usuario FELIPE VALVERDE nesta skill vale como validacao quando o extrator retornar exatamente esses dados.
3. Nao existem posicoes/conflitos que tornem o reload inseguro.
4. JSON, schema, parametros e estrategia foram validados.
5. Nao ha evento de alto impacto iminente nem bloqueio de qualidade/sessao/intervencao/rollover. Spread alto isolado nao e bloqueio se houver filtro operacional coerente.

Se a conta for real, exija segunda confirmacao explicita que identifique a conta e reconheca risco de ordens reais.

Antes da promocao, copie o config ativo para a pasta do relatorio com timestamp e sufixo `.bak.json`. Grave arquivo temporario no mesmo volume e substitua atomicamente. Se qualquer condicao falhar, mantenha o ativo intacto e registre o motivo.

### 11. Finalizar o relatorio

Siga `references/report-template.md`. Termine o documento com a secao `Proxima analise obrigatoria`.

Escolha o primeiro gatilho aplicavel:

- 15 minutos depois do proximo evento de alto impacto;
- transicao de sessao relevante;
- no maximo 4 horas durante operacao intraday;
- reabertura do mercado apos fim de semana/feriado;
- rollover diario se vier antes e afetar liquidez/spread.

Inclua gatilhos antecipados: movimento maior que 1 ATR H1, rompimento de nivel-chave, mudanca de regime, spread anormal sem filtro, intervencao/alerta MoF/BoJ, dados stale ou noticia material.

### 12. Verificar a entrega

Confirme antes de concluir:

- relatorio novo segue o nome exato e nao sobrescreveu o anterior;
- todas as afirmacoes materiais possuem fonte ou `[NAO CONFIRMADO]`;
- horarios e timeframes estao claros;
- contas matematicas podem ser reproduzidas;
- estrategia pertence ao catalogo e e compativel com o codigo atual;
- candidato e JSON valido e permanece desabilitado quando nao autorizado;
- config ativo nao mudou sem cumprir todos os gates;
- relatorio termina com data/hora da proxima analise.
