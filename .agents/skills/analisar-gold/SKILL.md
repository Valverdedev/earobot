---
name: analisar-gold
description: Analise profissional e auditavel de GOLD/XAUUSD para este robo, combinando Price Action, indicadores, dados do broker, noticias e fundamentos com fontes. Use ao gerar ou atualizar relatorios de ouro, decidir entre manter ou refazer a tese fundamental, selecionar uma estrategia registrada em docs/catalogo-estrategias.md, preparar um config candidato e, sob autorizacao e controles de seguranca, promover o config para D:\SistemEarobot\config.
---

# Analisar GOLD

## Objetivo

Produza uma decisao operacional rastreavel para o simbolo de ouro do broker. Trate a saida como apoio a decisao, nunca como garantia de retorno. Admita `NAO OPERAR` quando dados, contexto, liquidez, risco de evento ou compatibilidade do codigo forem insuficientes.

Use as lentes publicamente documentadas de Al Brooks, Alexandre Wolwacz (Stormer) e Fabricio Lorenz. Nao imite a voz dos especialistas, nao atribua a eles uma recomendacao especifica e nao invente regras proprietarias.

Leia antes de iniciar:

- `references/gold-market-framework.md`
- `references/report-template.md`
- `D:\SistemEarobot\financial.robot\docs\catalogo-estrategias.md`

## Caminhos fixos

- Relatorios: `D:\SistemEarobot\relatorios\GOLD`
- Prompt operacional: `D:\SistemEarobot\relatorios\GOLD\prompt_analise_gold.md`
- Extrator: `D:\SistemEarobot\financial.robot\publish-extractor\Financial.Robot.Extractor.exe`
- Catalogo: `D:\SistemEarobot\financial.robot\docs\catalogo-estrategias.md`
- Config ativo: `D:\SistemEarobot\config\GOLD.config.json`
- Terminal padrao: `activtraders`
- Simbolo padrao do broker: `GOLD`

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
6. Nao presuma que especificacoes do futuro de ouro da CME sejam iguais as do CFD `GOLD` do broker. Use os metadados extraidos do simbolo para point size, spread, volume e horario.
7. Nao force uma estrategia. `Nenhuma estrategia / operar=false` e uma decisao valida.
8. Nao aumente lote, risco, drawdown, numero de posicoes, metas globais ou permissao de operar sem autorizacao explicita.
9. Nao altere o config ativo apenas para concluir a tarefa. A gravacao nele pode acionar hot reload e ordens.
10. Nao alegue rentabilidade, robustez estatistica ou adequacao ao vivo sem backtest, custos realistas, teste fora da amostra e verificacao operacional.

## Fluxo obrigatorio

### 1. Preparar a execucao

Execute:

```powershell
& "D:\SistemEarobot\financial.robot\.agents\skills\analisar-gold\scripts\Prepare-GoldAnalysis.ps1" -RunExtractor
```

O script localiza o relatorio anterior por nome exato, prepara os nomes da nova execucao e extrai `M1,M5,M15,H1,H4,D1` com 500 candles. Capture o objeto retornado. O relatorio deve se chamar `yyyy-MM-dd_HH-mm-ss_gold.md`; nao use dois-pontos no nome.

Se o extrator falhar, pare a recomendacao operacional. Documente o erro e produza somente diagnostico, sem config ativo.

### 2. Ler o estado anterior

Leia integralmente o relatorio anterior, quando existir, o JSON extraido e o config ativo. Nao trate o prompt, extracoes JSON ou configs candidatos como relatorios anteriores.

Registre:

- caminho e timestamp do relatorio anterior;
- tese anterior e suas invalidacoes;
- estrategia/config anteriores;
- eventos previstos desde a analise anterior;
- diferencas observadas no mercado e nos dados.

### 3. Decidir sobre a analise fundamental

Atualize sempre cotacao do broker, qualidade dos dados, agenda e noticias. Refaça a tese fundamental completa quando ocorrer qualquer item:

- nao existe relatorio anterior;
- analise anterior tem mais de 6 horas;
- houve CPI, PPI, payroll, FOMC, decisao de juros, coletiva relevante, dado de atividade/inflacao material ou leilao do Tesouro com impacto;
- houve choque geopolitico, fiscal, bancario, cambial ou acao oficial relevante;
- o preco se deslocou pelo menos 1 ATR de H1 desde a referencia;
- mudou o regime tecnico de H1/H4 ou houve rompimento de nivel de invalidacao;
- iniciou nova sessao relevante, nova semana, ou ocorreu fechamento/reabertura de fim de semana;
- as fontes anteriores estao incompletas, conflitantes ou indisponiveis.

Mantenha e revalide a tese somente quando ela tiver no maximo 2 horas, nenhum gatilho acima tiver ocorrido e as fontes continuarem atuais. Mesmo nesse caso, pesquise o que mudou e rotule cada item como `mantido`, `atualizado` ou `invalidado`. Entre 2 e 6 horas, use julgamento conservador e justifique.

### 4. Validar os dados do broker

Antes da leitura grafica, confira:

- idade do tick e do ultimo candle;
- ordem temporal, duplicidades e gaps;
- quantidade de candles por timeframe;
- bid, ask, point size, digits e spread;
- posicoes abertas, margem livre e eventuais flags de risco;
- continuidade suficiente para EMA, RSI, ATR e VWAP.

Se houver dados stale, spread invalido, gaps materiais ou amostra insuficiente, reduza a confianca. Bloqueie novas entradas quando a falha puder mudar a direcao, os niveis ou o dimensionamento.

Spread alto, sozinho, nao deve ser usado como motivo para alterar o config global para `operar=false` quando ja existir filtro operacional de spread minimo/maximo capaz de bloquear a entrada no momento da execucao. Nesse caso, documente o spread, confirme que o filtro existe e esta coerente com ATR/stop, e deixe que o motor bloqueie a ordem se o spread continuar inadequado. So use spread como bloqueio de promocao ou motivo para `operar=false` quando ele estiver invalido, anormal de forma persistente, sem filtro operacional adequado, ou capaz de distorcer stop, alvo, nivel ou dimensionamento.

### 5. Pesquisar o contexto fundamental

Pesquise fontes atuais na internet. Priorize fontes primarias: Federal Reserve, BLS, BEA, US Treasury, FRED, CFTC, CME, World Gold Council, LBMA e orgaos oficiais. Use Reuters ou AP para noticia em tempo real e geopolitica, preferencialmente com corroboracao.

Construa uma tabela com `fato`, `impacto provavel`, `horizonte`, `fonte`, `publicado/evento em`, `acessado em` e `status`. Nao confunda expectativa de mercado com dado realizado. Quando houver conflito, apresente as versoes e reduza a confianca.

Avalie no minimo:

- dolar e condicoes financeiras;
- juros nominais e reais dos EUA;
- expectativa de politica monetaria;
- inflacao, emprego e atividade;
- risco geopolitico e demanda defensiva;
- fluxos/posicionamento quando houver fonte atual;
- demanda oficial de bancos centrais como contexto estrutural, sem transforma-la automaticamente em sinal intraday;
- calendario de alto impacto e janela ate o proximo evento.

### 6. Calcular e ler o grafico

Calcule as metricas de `references/gold-market-framework.md` a partir do JSON. Faca leitura top-down nesta ordem: `D1 -> H4 -> H1 -> M15 -> M5 -> M1`.

Classifique em cada timeframe:

- tendencia, range amplo, range estreito, transicao ou dados inconclusivos;
- estrutura de topos e fundos;
- impulso, correcao, compressao e expansao;
- rompimento, continuidade, pullback/reteste e falha de rompimento;
- suportes, resistencias, VWAP e distancias em ATR;
- qualidade das barras: corpo, pavios, sobreposicao e fechamento;
- alinhamento ou divergencia entre EMA 9/21/50, RSI 14, ATR 14 e VWAP.

Indicadores confirmam ou contradizem a estrutura; nenhum indicador isolado substitui contexto e Price Action. Calcule MACD apenas se houver serie completa e formula declarada.

### 7. Aplicar as tres lentes

Apresente as lentes separadamente e depois consolide consenso e divergencias:

- **Brooks:** decida primeiro tendencia versus trading range; avalie canal, tight/broad range, always-in, follow-through, pullbacks, segundas tentativas e rompimentos falhos.
- **Stormer:** exija contexto, gatilho objetivo e replicavel, invalidacao clara, risco conhecido e disciplina de execucao; trate padroes como hipoteses estatisticas, nao certezas.
- **Lorenz:** avalie forca dos candles, impulso/correcao, nascimento ou exaustao de tendencia, pontos relevantes, linhas de tendencia, rompimento/pullback e indicadores apenas como apoio.

Nao declare que um especialista `compraria` ou `venderia`. Declare apenas o que a lente metodologica sugere no conjunto de dados observado.

### 8. Montar cenarios

Produza no minimo cenarios altista, baixista e neutro. Para cada um, informe:

- condicoes necessarias;
- gatilho observavel;
- nivel de invalidacao;
- objetivos tecnicos, sem prometer alcance;
- riscos de evento e spread;
- timeframe de validade;
- estrategia candidata ou `nenhuma`.

Nao converta o cenario base em ordem quando a relacao risco/retorno, o spread ou a proximidade de noticia de alto impacto forem inadequados.

Se houver evento de alto impacto nos proximos 30 minutos, mantenha o candidato com `operar=false`, salvo regra operacional mais restritiva ja existente. Reavalie 15 minutos depois da divulgacao ou somente quando spread e estrutura normalizarem, o que ocorrer por ultimo.

### 9. Selecionar uma estrategia implementada

Releia o catalogo a cada execucao. Escolha somente estrategia existente no codigo e compare o regime exigido com o regime observado. Prefira uma unica estrategia ativa para evitar sobreposicao de sinais e risco.

Respeite as restricoes atualmente catalogadas:

- nao selecione `ScalperWinPullbackCurto` enquanto a retencao do motor nao suportar os 105 candles exigidos;
- nao use `PriceActionSuporteResistencia` em modo reteste enquanto a conversao booleana documentada nao for corrigida e testada;
- trate `AberturaFaixaRompimento` como agressiva e sensivel a reinicio/historico;
- calibre canais e niveis fixos de `HydrusEmaChannelBreakout` com dados atuais do simbolo;
- use `ReversaoRange` apenas em range estabelecido;
- use `CruzamentoEma` somente em inicio/retomada de tendencia com confirmacao, evitando lateralidade.

Se o codigo ou catalogo tiver mudado, investigue novamente e atualize a conclusao com referencias de arquivo/linha. Nao contorne silenciosamente uma limitacao de codigo.

### 10. Preparar o config candidato

Parta do config ativo e altere o minimo necessario. Preserve campos desconhecidos, magic numbers, limites de risco e configuracoes globais. Salve o candidato no caminho indicado pelo script, dentro da pasta de relatorios, com `operar=false` por padrao.

Inclua no relatorio um diff sem segredos com:

- estrategia removida, mantida ou adicionada;
- parametros alterados e justificativa;
- campos preservados;
- riscos e dependencias;
- condicao exata para ativacao.

Promova para `D:\SistemEarobot\config\GOLD.config.json` somente quando todas as condicoes forem verdadeiras:

1. O usuario autorizou explicitamente a ativacao nesta execucao.
2. Uma fonte independente e autoritativa confirmou o modo e a identidade da conta; para a conta demo validada `6252382` / `ActivTradesCorp-Server` / `Felipe Valverde`, a confirmacao do usuario FELIPE VALVERDE nesta skill vale como validacao quando o extrator retornar exatamente esses dados.
3. Nao existem posicoes/conflitos que tornem o reload inseguro.
4. JSON, schema, parametros e estrategia foram validados.
5. Nao ha evento de alto impacto iminente nem bloqueio de qualidade/spread. Spread alto isolado nao e bloqueio se houver filtro operacional de spread coerente e validado para impedir entradas no momento da ordem.

Se a conta for real, exija uma segunda confirmacao explicita que identifique a conta e reconheca que a promocao pode enviar ordens reais. Uma autorizacao generica para criar a skill, analisar ou preparar o config nao atende esse requisito.

Antes da promocao, copie o config ativo para a pasta do relatorio com timestamp e sufixo `.bak.json`. Grave um arquivo temporario no mesmo volume e substitua atomicamente. Se qualquer condicao falhar, mantenha o ativo intacto e registre o motivo.

### 11. Finalizar o relatorio

Siga `references/report-template.md`. Termine o documento com a secao `Proxima analise obrigatoria`.

Escolha o primeiro gatilho aplicavel:

- 15 minutos depois do proximo evento de alto impacto;
- transicao de sessao relevante para o plano;
- no maximo 4 horas durante operacao intraday;
- reabertura do mercado apos fim de semana.

Inclua tambem gatilhos antecipados: movimento maior que 1 ATR H1, rompimento de nivel-chave, mudanca de regime, spread anormal, dado stale ou noticia material. O horario deve ser absoluto, com fuso; nao escreva apenas `daqui a 4 horas`.

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
