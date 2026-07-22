# Estrutura de mercado e pesquisa para EURUSD

## 1. Modelo mental do EUR/USD

Trate EUR/USD como preco relativo entre euro e dolar, nao como ativo isolado. Organize a tese em cinco familias:

1. **Diferencial de politica monetaria:** Fed versus ECB, guidance, expectativas de juros, curva de yields e condicoes financeiras.
2. **Diferencial de crescimento:** EUA versus zona do euro, PMI/ISM, PIB, emprego, renda, consumo e producao.
3. **Inflacao e juros reais:** CPI/PCE/PPI nos EUA, HICP/PPI na zona do euro, salarios e yields reais quando disponiveis.
4. **Risco, dolar e fluxos:** demanda defensiva por USD, risco geopolitico, energia, fiscal, bancos e posicionamento.
5. **Momentum e microestrutura:** tendencia, compressao/expansao, sessoes Londres/New York, liquidez, spread e stops.

Em EUR/USD, alta do par significa euro forte ou dolar fraco. Baixa do par significa euro fraco ou dolar forte. Declare sempre qual lado da equacao esta dominando.

## 2. Fontes prioritarias

Use links diretos para documento, release, calendario ou pagina consultada.

| Tema | Fonte primaria preferida |
|---|---|
| Politica monetaria EUA | Federal Reserve: https://www.federalreserve.gov/monetarypolicy/fomccalendars.htm |
| Juros EUA | Federal Reserve H.15: https://www.federalreserve.gov/releases/h15/ |
| CPI/PPI/emprego EUA | BLS: https://www.bls.gov/schedule/2026/home.htm |
| PIB/PCE/renda EUA | BEA: https://www.bea.gov/news/schedule/ |
| Treasury e leiloes | TreasuryDirect: https://treasurydirect.services.treasury.gov/auctions/when-auctions-happen/ |
| Politica monetaria zona do euro | ECB: https://www.ecb.europa.eu/press/calendars/mgcgc/html/index.en.html |
| Estatisticas e calendario eurozona | Eurostat: https://ec.europa.eu/eurostat/news/euro-indicators/release-calendar |
| HICP zona do euro | Eurostat/ECB calendar: https://www.ecb.europa.eu/press/calendars/statscal/ges/html/sthicp.en.html |
| Posicionamento futuro | CFTC COT: https://www.cftc.gov/MarketReports/index.htm |
| Mercado futuro EUR/USD | CME Euro FX: https://www.cmegroup.com/markets/fx/g10/euro-fx.html |

Para eventos geopoliticos e noticias em tempo real, use Reuters ou AP e procure confirmacao oficial. Nao use post social, agregador sem autoria, blog promocional ou snippet de busca como evidencia final.

## 3. Agenda que merece verificacao

Verifique horario, consenso, realizado, revisao e fonte para:

- FOMC, ECB, atas/minutas, coletivas e discursos de membros relevantes;
- CPI, PPI, PCE, payroll, desemprego, salarios, jobless claims, retail sales, ISM e PMI dos EUA;
- HICP, PMI, PIB, desemprego, salarios, producao, vendas e confianca na zona do euro;
- dados fiscais, leiloes e movimentos materiais de yields;
- risco energetico europeu, geopolitica, bancos e choques de liquidez;
- publicacoes CFTC/COT para Euro FX, respeitando defasagem.

Eventos proximos a sessoes de alta liquidez podem gerar falsos rompimentos. Trate `London open`, `New York open` e overlap Londres/New York como janelas que podem alterar volatilidade, mesmo sem noticia.

## 4. Calculos reproduziveis

Use valores do broker e informe unidades. Em EURUSD, `point_size` pode ser 0.00001 em brokers de 5 digitos; nao assuma.

```text
mid = (bid + ask) / 2
spread_abs = ask - bid
spread_points = spread_abs / point_size
spread_pips = spread_abs / pip_size
pip_size = 0.0001 quando digits >= 4, salvo metadado do broker indicar outra convencao
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

Nao divida por zero. Quando ATR, point size, pip size ou range forem invalidos, marque a metrica indisponivel e bloqueie qualquer decisao que dependa dela.

Para medir deslocamento desde o relatorio anterior:

```text
move_H1_ATR = abs(mid_atual - mid_anterior) / ATR_H1_referencia
```

Para risco monetario, use tick size, tick value, volume step, stop distance e moeda da conta fornecidos pelo broker; nao use a especificacao do contrato CME como substituto.

## 5. Leitura Price Action por regime

### Tendencia definida

Procure sequencia coerente de topos/fundos, barras de impulso, pullbacks contidos, pouca sobreposicao, EMA alinhada e follow-through depois de rompimentos. Em EUR/USD, confirme se o movimento ocorre em janela liquida e se nao depende apenas de fraqueza momentanea de uma perna.

### Range amplo

Procure extremos respeitados, falsas rupturas, sobreposicao e retornos ao valor/VWAP. Favoreca logica de comprar baixo e vender alto somente com rejeicao clara, espaco ate o centro/oposto e risco definido. Evite entradas no meio.

### Range estreito/compressao

Presuma baixa vantagem ate ocorrer rompimento com qualidade e continuidade. Primeiro rompimento antes de noticia, abertura ou fix pode ser armadilha. Spread pequeno nao compensa falta de amplitude.

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
- fundamentos nao contradizendo materialmente a direcao intraday;
- espaco suficiente frente a spread, stop e nivel oposto;
- ausencia de evento iminente capaz de invalidar o setup;
- estrategia implementada compativel com o regime.

Reduza a confianca por divergencia entre timeframes, fonte conflitante, spread elevado, liquidez atipica, proximidade de noticia, restart do motor, niveis fixos desatualizados ou limitacao conhecida do codigo.
