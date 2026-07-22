# Estrutura de mercado e pesquisa para GOLD

## 1. Modelo mental do ouro

Trate o ouro como um ativo com drivers concorrentes, nao como uma funcao unica do dolar ou dos juros. Organize a tese nas quatro familias usadas pelo Gold Return Attribution Model do World Gold Council:

1. **Expansao economica:** renda, consumo, joalheria e demanda tecnologica.
2. **Risco e incerteza:** demanda defensiva, risco geopolitico, financeiro e fiscal.
3. **Custo de oportunidade:** juros reais e nominais, dolar e atratividade relativa de ativos remunerados.
4. **Momentum:** fluxo, posicionamento, tendencia e comportamento tecnico.

Considere compras de bancos centrais separadamente como componente estrutural quando os dados forem publicados. Nao use dado trimestral de demanda oficial como gatilho de M1/M5.

Relacoes tradicionais podem enfraquecer ou inverter temporariamente. Juros reais mais baixos e dolar mais fraco costumam favorecer o ouro, mas fluxo defensivo, compras oficiais, liquidez e momentum podem dominar. Declare quando uma conclusao for inferencia.

## 2. Fontes prioritarias

Use links diretos para o documento ou release consultado, nao apenas para a pagina inicial.

| Tema | Fonte primaria preferida |
|---|---|
| Modelo de drivers e demanda | World Gold Council: https://www.gold.org/goldhub/research/gold-outlook-2026 |
| Estrutura do mercado | WGC market primer: https://www.gold.org/goldhub/research/market-primer/gold-market-primer-market-size-and-structure |
| Demanda de bancos centrais | WGC Gold Demand Trends: https://www.gold.org/goldhub/research/gold-demand-trends/gold-demand-trends-q1-2026/central-banks |
| Juros reais EUA | FRED DFII10: https://fred.stlouisfed.org/series/DFII10 |
| Politica monetaria | Federal Reserve: https://www.federalreserve.gov/monetarypolicy/fomccalendars.htm |
| CPI/PPI/emprego | BLS: https://www.bls.gov/schedule/news_release/cpi.htm e https://www.bls.gov/schedule/2026/home.htm |
| PIB/PCE/renda | BEA: https://www.bea.gov/news/schedule/ |
| Leiloes do Tesouro | TreasuryDirect: https://treasurydirect.services.treasury.gov/auctions/when-auctions-happen/ |
| Posicionamento futuro | CFTC COT: https://www.cftc.gov/MarketReports/index.htm |
| Mercado futuro e eventos | CME Gold: https://www.cmegroup.com/markets/metals/precious/gold-futures.html |
| Benchmark | LBMA: https://www.lbma.org.uk/prices-and-data/precious-metal-prices |

Para eventos geopoliticos e noticias em tempo real, use Reuters ou AP e procure confirmacao oficial. Nao use post social, agregador sem autoria, blog promocional ou snippet de busca como evidencia final.

## 3. Agenda que merece verificacao

Verifique horario, consenso, realizado, revisao e fonte para:

- FOMC, atas e discursos com potencial de mudar a trajetoria de juros;
- CPI, PPI, payroll, desemprego, salarios e pedidos de seguro-desemprego;
- PCE, PIB, vendas, ISM e outros dados de atividade relevantes;
- leiloes do Tesouro e movimentos materiais de yields;
- eventos geopoliticos, sancoes e riscos ao sistema financeiro;
- publicacoes de fluxo, COT e demanda oficial, respeitando a defasagem.

A CME destaca politica monetaria dos EUA, CPI/PPI/payroll e estabilidade economica/geopolitica como fatores a monitorar no ouro. Use isso como lista de verificacao, nao como previsao automatica.

## 4. Calculos reproduziveis

Use valores do broker e informe unidades.

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
```

Nao divida por zero. Quando ATR, point size ou range forem invalidos, marque a metrica indisponivel e bloqueie qualquer decisao que dependa dela.

Para medir deslocamento desde o relatorio anterior:

```text
move_H1_ATR = abs(mid_atual - mid_anterior) / ATR_H1_referencia
```

Declare qual ATR foi usado. Para risco monetario, use tick size, tick value, volume step, stop distance e moeda da conta fornecidos pelo broker; nao use a especificacao do contrato CME como substituto.

## 5. Leitura Price Action por regime

### Tendencia definida

Procure sequencia coerente de topos/fundos, barras de impulso, pullbacks contidos, pouca sobreposicao, EMA alinhada e follow-through depois de rompimentos. Diferencie tendencia forte de canal exausto. Uma tendencia forte favorece continuidade; um canal maduro perto de resistencia/suporte maior exige cautela.

### Range amplo

Procure extremos respeitados, falsas rupturas, sobreposicao e retornos ao valor/VWAP. Favoreca logica de comprar baixo e vender alto somente com rejeicao clara, espaco ate o centro/oposto e risco definido. Evite entradas no meio.

### Range estreito/compressao

Presuma baixa vantagem ate ocorrer rompimento com qualidade e continuidade. Um primeiro rompimento sem follow-through pode ser armadilha. Spread e noticia proxima podem consumir a vantagem.

### Transicao

Procure rompimento da estrutura anterior, reteste/pullback e segunda tentativa. Exija evidencia adicional porque algoritmos de tendencia e reversao podem receber sinais conflitantes.

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
- regime claro em H1/H4 e gatilho coerente em M15/M5/M1;
- Price Action e indicadores nao materialmente conflitantes;
- espaco suficiente frente a spread, stop e nivel oposto;
- ausencia de evento iminente capaz de invalidar o setup;
- estrategia implementada compativel com o regime.

Reduza a confianca por divergencia entre timeframes, fonte conflitante, spread elevado, liquidez atipica, proximidade de noticia, restart do motor, niveis fixos desatualizados ou limitacao conhecida do codigo.

