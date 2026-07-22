# Estrutura de mercado e pesquisa para DAX/Ger40Sep26

## 1. Modelo mental do DAX

Trate `Ger40Sep26` como o simbolo do broker para exposicao ao DAX alemao, nao como o indice cash oficial nem como o futuro Eurex FDAX sem verificacao. O DAX e um indice de acoes alemas blue chip e e sensivel a fatores de risco europeus e globais:

1. **Risco de acoes europeias:** fluxo para/fora de equities, Euro Stoxx/Stoxx 600, volatilidade e correlacao com futuros dos EUA.
2. **Politica monetaria e juros:** ECB, Fed, yields de bunds e Treasuries, condicoes financeiras e custo de capital.
3. **Crescimento e ciclo alemao/eurozona:** PMI, Ifo, ZEW, PIB, producao industrial, encomendas, vendas e emprego.
4. **Moeda, energia e exportadoras:** EURUSD, gas/energia, China/global trade e demanda externa.
5. **Microestrutura:** abertura Xetra/Frankfurt, Londres, New York, gaps, rolagem do contrato/simbolo datado, spread e volatilidade intraday.

Alta do DAX geralmente reflete maior apetite por risco e/ou melhora de expectativas sobre lucros/ciclo europeu. Baixa pode refletir aversao a risco, yields pressionando valuation, dados fracos, choque energetico/geopolitico ou fraqueza global.

## 2. Fontes prioritarias

Use links diretos para documento, release, calendario ou pagina consultada.

| Tema | Fonte primaria preferida |
|---|---|
| Indice DAX e metodologia | STOXX/Qontigo: https://stoxx.com/index/dax/ |
| Bolsa/mercado alemao | Deutsche Boerse: https://www.deutsche-boerse.com/ |
| Futuro DAX | Eurex: https://www.eurex.com/ex-en/markets/idx/dax |
| Politica monetaria zona do euro | ECB: https://www.ecb.europa.eu/press/calendars/mgcgc/html/index.en.html |
| Calendario estatistico ECB | ECB: https://www.ecb.europa.eu/press/calendars/statscal/html/index.en.html |
| Alemanha estatisticas | Destatis: https://www.destatis.de/EN/Press/PressCalendar/_node.html |
| Eurozona estatisticas | Eurostat: https://ec.europa.eu/eurostat/news/euro-indicators/release-calendar |
| Bundesbank/yields | Bundesbank: https://www.bundesbank.de/en/statistics |
| Ifo Business Climate | ifo Institute: https://www.ifo.de/en/facts |
| ZEW sentiment | ZEW: https://www.zew.de/en/press/latest-press-releases |
| PMI Alemanha/eurozona | S&P Global PMI: https://www.pmi.spglobal.com/ |
| Politica monetaria EUA | Federal Reserve: https://www.federalreserve.gov/monetarypolicy/fomccalendars.htm |
| CPI/PPI/emprego EUA | BLS: https://www.bls.gov/schedule/ |
| PIB/PCE/renda EUA | BEA: https://www.bea.gov/news/schedule/ |

Para eventos geopoliticos e noticias em tempo real, use Reuters ou AP e procure confirmacao oficial. Nao use post social, agregador sem autoria, blog promocional ou snippet de busca como evidencia final.

## 3. Agenda que merece verificacao

Verifique horario, consenso, realizado, revisao e fonte para:

- reunioes ECB/Fed, atas, coletivas e discursos relevantes;
- CPI/HICP, PPI, PMI, Ifo, ZEW, PIB, producao industrial, encomendas, vendas, desemprego e salarios da Alemanha/zona do euro;
- dados dos EUA que movem risco global: payroll, CPI, PCE, ISM, retail sales, jobless claims e FOMC;
- leiloes/yields relevantes, spreads soberanos, choques bancarios ou fiscais;
- energia, gas, geopolitica europeia, China/global trade e noticias corporativas/setoriais materiais;
- vencimento/rolagem do simbolo `Ger40Sep26`, feriados europeus e horario de verao/inverno.

Trate a abertura cash europeia como janela de risco operacional: o primeiro rompimento pode falhar, spreads podem alargar e gaps podem distorcer indicadores curtos.

## 4. Calculos reproduziveis

Use valores do broker e informe unidades. Em indices, `point_size`, `tick value`, `digits` e `volume step` variam por broker; nao assuma especificacao Eurex.

```text
mid = (bid + ask) / 2
spread_abs = ask - bid
spread_points = spread_abs / point_size
spread_atr_M1 = spread_abs / ATR_M1
spread_atr_M5 = spread_abs / ATR_M5
ATR_percent = ATR / mid * 100
distance_level_atr = abs(mid - level) / ATR_do_timeframe
ema_separation_atr = abs(EMA_rapida - EMA_lenta) / ATR
candle_range = high - low
body = abs(close - open)
body_ratio = body / candle_range
upper_wick_ratio = (high - max(open, close)) / candle_range
lower_wick_ratio = (min(open, close) - low) / candle_range
close_location = (close - low) / candle_range
gap_abs = abertura_atual - fechamento_anterior
gap_atr = abs(gap_abs) / ATR_do_timeframe
```

Nao divida por zero. Quando ATR, point size, range ou metadados forem invalidos, marque a metrica indisponivel e bloqueie qualquer decisao que dependa dela.

Para medir deslocamento desde o relatorio anterior:

```text
move_H1_ATR = abs(mid_atual - mid_anterior) / ATR_H1_referencia
```

Para risco monetario, use tick size, tick value, volume step, stop distance e moeda da conta fornecidos pelo broker.

## 5. Leitura Price Action por regime

### Tendencia definida

Procure sequencia coerente de topos/fundos, barras de impulso, pullbacks contidos, pouca sobreposicao, EMA alinhada e follow-through. No DAX, confirme se a tendencia ocorre em janela liquida e se nao e apenas arrasto temporario de futuros dos EUA ou gap de abertura.

### Range amplo

Procure extremos respeitados, falsas rupturas, sobreposicao e retorno ao VWAP/valor. Em indice, range amplo apos abertura pode favorecer reversao nos extremos, mas exige espaco suficiente contra spread e stop.

### Range estreito/compressao

Presuma baixa vantagem ate ocorrer rompimento com qualidade e continuidade. Compressao antes de abertura cash, dado macro ou New York open pode gerar falso rompimento.

### Transicao

Procure rompimento da estrutura anterior, reteste/pullback e segunda tentativa. Exija evidencia adicional quando D1/H4 apontam direcao diferente de M5/M15.

## 6. Lentes metodologicas publicas

### Al Brooks

Use a arvore de decisao publica como enquadramento: primeiro trading range versus tendencia; depois tight/broad range, breakout/channel e forca do always-in. Avalie follow-through, pullbacks, segundas tentativas e falhas.

- Site oficial: https://www.brookspriceaction.com/
- Arvore de decisao: https://www.brookstradingcourse.com/wp-content/uploads/wpforo/attachments/19757/2509-Price-Action-Decision-Tree-graphic.pdf

### Alexandre Wolwacz (Stormer)

Use contexto, setup objetivo e replicavel, risco, disciplina e consistencia sobre uma serie de trades. Padroes de reversao ou movimento frustrado sao hipoteses que precisam de gatilho e invalidacao.

- Perfil/metodo publicado: https://www.infomoney.com.br/mercados/stormer-vence-1a-edicao-do-premio-top-traders-infomoney/

### Fabricio Lorenz

Use forca do candle, impulso/correcao, nascimento/exaustao da tendencia, pontos relevantes, linhas de tendencia, rompimento/pullback e indicadores como apoio. Nao tente reconstruir material pago ou regra nao publicada.

- Pagina publica do metodo: https://lorenzfabricio.com.br/epa-tp-c/

## 7. Confluencia e confianca

Classifique a confianca como `baixa`, `moderada` ou `alta`, nunca como probabilidade numerica sem modelo calibrado. Para confianca alta, exija:

- dados integros e atuais;
- simbolo `Ger40Sep26` liquido, sem risco imediato de rolagem/vencimento;
- regime claro em H1/H4 e gatilho coerente em M15/M5/M1;
- Price Action e indicadores nao materialmente conflitantes;
- fundamentos e risco global nao contradizendo materialmente a direcao intraday;
- espaco suficiente frente a spread, stop e nivel oposto;
- ausencia de evento iminente capaz de invalidar o setup;
- estrategia implementada compativel com o regime.

Reduza a confianca por divergencia entre timeframes, fonte conflitante, spread elevado sem filtro adequado, liquidez atipica, proximidade de noticia, abertura cash europeia, restart do motor, rolagem, niveis fixos desatualizados ou limitacao conhecida do codigo.

