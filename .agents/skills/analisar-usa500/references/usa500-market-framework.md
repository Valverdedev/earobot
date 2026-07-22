# Estrutura de mercado e pesquisa para Usa500

## 1. Modelo mental do Usa500

Trate `Usa500` como o simbolo do broker para exposicao ao S&P 500, nao como o indice cash oficial, futuro E-mini da CME ou ETF SPY sem verificacao. O ativo tende a reagir a:

1. **Fed e juros:** FOMC, speeches, yields, curva, dolar e condicoes financeiras.
2. **Macro EUA:** CPI, PPI, PCE, payroll, jobless claims, ISM/PMI, retail sales, GDP e credito.
3. **Earnings e mega caps:** tecnologia, IA/semicondutores, guidance, margens e concentracao do indice.
4. **Risco global:** VIX, credit spreads, oil, geopolitica, tarifas, fiscal e fluxo de risco.
5. **Microestrutura:** pre-market, cash open NYSE/Nasdaq, lunch, power hour, after-hours, feriados, meia sessao, gaps e rollover/ajuste do broker.

Alta do Usa500 geralmente reflete apetite por risco, queda de yields reais, earnings/guidance melhores, liquidez favoravel ou alivio macro. Baixa pode refletir yields pressionando valuation, inflacao, Fed hawkish, recessao/credito, choques geopoliticos ou queda em mega caps.

## 2. Fontes prioritarias

| Tema | Fonte primaria preferida |
|---|---|
| S&P 500 metodologia | S&P Dow Jones Indices: https://www.spglobal.com/spdji/ |
| Futuros ES / calendario | CME Group: https://www.cmegroup.com/ |
| Politica monetaria EUA | Federal Reserve: https://www.federalreserve.gov/monetarypolicy/fomccalendars.htm |
| CPI/PPI/emprego | BLS: https://www.bls.gov/schedule/ |
| GDP/PCE/renda | BEA: https://www.bea.gov/news/schedule/ |
| Retail sales e dados Census | U.S. Census: https://www.census.gov/economic-indicators/ |
| ISM | ISM: https://www.ismworld.org/supply-management-news-and-reports/reports/ism-report-on-business/ |
| PMI | S&P Global PMI: https://www.pmi.spglobal.com/ |
| Treasury/yields/auctions | U.S. Treasury: https://home.treasury.gov/ |
| VIX | Cboe: https://www.cboe.com/tradable_products/vix/ |
| Mercado e feriados | NYSE/Nasdaq: https://www.nyse.com/markets/hours-calendars |

Use Reuters ou AP para noticia em tempo real e geopolitica, preferencialmente com corroboracao oficial. Nao use post social, agregador sem autoria, blog promocional ou snippet de busca como evidencia final.

## 3. Agenda que merece verificacao

Verifique horario, consenso, realizado, revisao e fonte para:

- FOMC, atas, discursos do Fed, dot plot e Beige Book;
- CPI, PPI, PCE, payroll, jobless claims, JOLTS, ISM/PMI, retail sales, GDP, durable goods e consumer sentiment;
- leiloes do Treasury, yields 2y/10y/30y, DXY, credit spreads e VIX;
- earnings/guidance de mega caps e bancos, eventos de IA/semicondutores e regulacao;
- feriados dos EUA, meia sessao, cash open/close, expirations/quad witching e horarios especiais do broker.

Trate cash open e divulgacoes 08:30 ET/10:00 ET como janelas de risco operacional: spreads podem alargar, gaps podem distorcer indicadores curtos e falsos rompimentos sao frequentes.

## 4. Calculos reproduziveis

Use valores do broker e informe unidades. Em indices, `point_size`, `tick value`, `digits` e `volume step` variam por broker; nao assuma especificacao CME.

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

## 5. Leitura Price Action por regime

### Tendencia definida

Procure sequencia coerente de topos/fundos, barras de impulso, pullbacks contidos, pouca sobreposicao, EMA alinhada e follow-through. Confirme se nao e apenas gap de pre-market ou arrasto temporario de Nasdaq/mega caps.

### Range amplo

Procure extremos respeitados, falsas rupturas, sobreposicao e retorno ao VWAP/valor. Em indice americano, range apos abertura pode favorecer reversao nos extremos, mas exige espaco suficiente contra spread e stop.

### Range estreito/compressao

Presuma baixa vantagem ate ocorrer rompimento com qualidade e continuidade. Compressao antes de 08:30 ET, 10:00 ET, FOMC ou earnings pode gerar falso rompimento.

### Transicao

Procure rompimento da estrutura anterior, reteste/pullback e segunda tentativa. Exija evidencia adicional quando D1/H4 apontam direcao diferente de M5/M15.

## 6. Lentes metodologicas publicas

### Al Brooks

Use a arvore de decisao publica como enquadramento: primeiro trading range versus tendencia; depois tight/broad range, breakout/channel e forca do always-in. Avalie follow-through, pullbacks, segundas tentativas e falhas.

- Site oficial: https://www.brookspriceaction.com/
- Arvore de decisao: https://www.brookstradingcourse.com/wp-content/uploads/wpforo/attachments/19757/2509-Price-Action-Decision-Tree-graphic.pdf

### Alexandre Wolwacz (Stormer)

Use contexto, setup objetivo e replicavel, risco, disciplina e consistencia sobre uma serie de trades.

- Perfil/metodo publicado: https://www.infomoney.com.br/mercados/stormer-vence-1a-edicao-do-premio-top-traders-infomoney/

### Fabricio Lorenz

Use forca do candle, impulso/correcao, nascimento/exaustao da tendencia, pontos relevantes, linhas de tendencia, rompimento/pullback e indicadores como apoio.

- Pagina publica do metodo: https://lorenzfabricio.com.br/epa-tp-c/

## 7. Confluencia e confianca

Classifique a confianca como `baixa`, `moderada` ou `alta`, nunca como probabilidade numerica sem modelo calibrado. Para confianca alta, exija:

- dados integros e atuais;
- `Usa500` liquido e em sessao operacional normal;
- regime claro em H1/H4 e gatilho coerente em M15/M5/M1;
- Price Action e indicadores nao materialmente conflitantes;
- fundamentos EUA/risco global nao contradizendo materialmente a direcao intraday;
- espaco suficiente frente a spread, stop e nivel oposto;
- ausencia de evento iminente capaz de invalidar o setup;
- estrategia implementada compativel com o regime.

Reduza a confianca por divergencia entre timeframes, fonte conflitante, spread elevado sem filtro adequado, liquidez atipica, proximidade de noticia, cash open, FOMC, earnings, restart do motor, feriado/meia sessao, niveis fixos desatualizados ou limitacao conhecida do codigo.
