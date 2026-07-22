# Estrutura de mercado e pesquisa para HKInd

## 1. Modelo mental do HKInd

Trate `HKInd` como o simbolo do broker para exposicao a indice de Hong Kong, geralmente associado ao Hang Seng, nao como o indice cash oficial nem como o futuro HKEX sem verificacao. HKInd e sensivel a fatores de risco asiaticos, China continental, tecnologia chinesa, juros em USD e fluxo global:

1. **Risco China/Hong Kong:** politica economica chinesa, liquidez, setor imobiliario, regulacao, geopolitica e fluxo para equities chinesas.
2. **Juros e dolar:** Fed, US yields, USDHKD peg, CNH/CNY e condicoes financeiras globais.
3. **Crescimento e dados macro:** China e Hong Kong: PMI, CPI/PPI, PIB, retail sales, industrial production, credito, comercio exterior e emprego.
4. **Tecnologia e consumo:** Hang Seng Tech, ADRs chinesas, Nasdaq e noticias de regulacao ou semicondutores.
5. **Microestrutura:** abertura de Hong Kong, intervalo de almoco, reabertura, Londres/New York, feriados, gaps, spread e volatilidade intraday.

Alta do HKInd geralmente reflete maior apetite por risco China/HK, alivio regulatorio, estimulo, queda de yields globais ou melhora em tecnologia/consumo. Baixa pode refletir aversao a risco, pressao em tecnologia, dados chineses fracos, tensao EUA-China, estresse imobiliario ou alta de yields.

## 2. Fontes prioritarias

Use links diretos para documento, release, calendario ou pagina consultada.

| Tema | Fonte primaria preferida |
|---|---|
| Indices Hang Seng | Hang Seng Indexes: https://www.hsi.com.hk/ |
| Bolsa/futuros Hong Kong | HKEX: https://www.hkex.com.hk/ |
| Autoridade monetaria HK | HKMA: https://www.hkma.gov.hk/ |
| Estatisticas Hong Kong | C&SD: https://www.censtatd.gov.hk/ |
| Governo Hong Kong | https://www.info.gov.hk/ |
| Banco central China | PBoC: http://www.pbc.gov.cn/english/ |
| Estatisticas China | NBS China: https://www.stats.gov.cn/english/ |
| Comercio China | China Customs: http://english.customs.gov.cn/ |
| FX/reservas China | SAFE: https://www.safe.gov.cn/en/ |
| Politica monetaria EUA | Federal Reserve: https://www.federalreserve.gov/monetarypolicy/fomccalendars.htm |
| CPI/PPI/emprego EUA | BLS: https://www.bls.gov/schedule/ |
| PIB/PCE/renda EUA | BEA: https://www.bea.gov/news/schedule/ |

Para eventos geopoliticos e noticias em tempo real, use Reuters ou AP e procure confirmacao oficial. Nao use post social, agregador sem autoria, blog promocional ou snippet de busca como evidencia final.

## 3. Agenda que merece verificacao

Verifique horario, consenso, realizado, revisao e fonte para:

- decisoes/comunicados do PBoC, HKMA, Fed e dados de liquidez/credito chineses;
- China/HK CPI, PPI, PMI, PIB, retail sales, industrial production, fixed asset investment, trade balance, emprego e mercado imobiliario;
- dados dos EUA que movem risco global: payroll, CPI, PCE, ISM, retail sales, jobless claims e FOMC;
- USDHKD, CNH/CNY, yields dos EUA, tensao EUA-China, tarifas, semicondutores, tecnologia e noticias reguladoras;
- feriados de Hong Kong/China, meia sessao, typhoon/black rainstorm arrangements, horario de verao/inverno e horarios especiais do broker.

Trate a abertura de Hong Kong e o retorno do intervalo como janelas de risco operacional: o primeiro rompimento pode falhar, spreads podem alargar e gaps podem distorcer indicadores curtos.

## 4. Calculos reproduziveis

Use valores do broker e informe unidades. Em indices, `point_size`, `tick value`, `digits` e `volume step` variam por broker; nao assuma especificacao HKEX.

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

Procure sequencia coerente de topos/fundos, barras de impulso, pullbacks contidos, pouca sobreposicao, EMA alinhada e follow-through. No HKInd, confirme se a tendencia ocorre em janela liquida e se nao e apenas gap/arrasto temporario de China A-shares, Nasdaq ou noticia.

### Range amplo

Procure extremos respeitados, falsas rupturas, sobreposicao e retorno ao VWAP/valor. Em indice asiatico, range amplo apos abertura pode favorecer reversao nos extremos, mas exige espaco suficiente contra spread e stop.

### Range estreito/compressao

Presuma baixa vantagem ate ocorrer rompimento com qualidade e continuidade. Compressao antes de dado macro, reabertura do intervalo ou New York open pode gerar falso rompimento.

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
- `HKInd` liquido e em sessao operacional normal;
- regime claro em H1/H4 e gatilho coerente em M15/M5/M1;
- Price Action e indicadores nao materialmente conflitantes;
- fundamentos China/HK/EUA e risco global nao contradizendo materialmente a direcao intraday;
- espaco suficiente frente a spread, stop e nivel oposto;
- ausencia de evento iminente capaz de invalidar o setup;
- estrategia implementada compativel com o regime.

Reduza a confianca por divergencia entre timeframes, fonte conflitante, spread elevado sem filtro adequado, liquidez atipica, proximidade de noticia, abertura/reabertura, restart do motor, feriado/meia sessao, niveis fixos desatualizados ou limitacao conhecida do codigo.
