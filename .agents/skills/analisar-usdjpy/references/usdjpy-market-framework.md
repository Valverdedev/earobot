# Estrutura de mercado e pesquisa para USDJPY

## 1. Modelo mental do USDJPY

Trate `USDJPY` como par Forex do broker. O par tende a reagir a diferencial de juros EUA-Japao, expectativa Fed-BoJ, yields de Treasuries/JGBs, risco global e risco de intervencao cambial japonesa.

Alta de USDJPY geralmente reflete dolar forte, yields dos EUA subindo, BoJ relativamente dovish, carry favoravel ou aversao a risco com dolar dominante. Baixa pode refletir queda de yields dos EUA, BoJ hawkish, unwind de carry, intervencao/verbal intervention, risco global favorecendo yen ou dados japoneses fortes.

## 2. Fontes prioritarias

| Tema | Fonte primaria preferida |
|---|---|
| Fed/FOMC | https://www.federalreserve.gov/monetarypolicy/fomccalendars.htm |
| CPI/PPI/emprego EUA | https://www.bls.gov/schedule/ |
| GDP/PCE EUA | https://www.bea.gov/news/schedule/ |
| Treasury/yields | https://home.treasury.gov/ |
| BoJ | https://www.boj.or.jp/en/ |
| MoF Japan / FX intervention | https://www.mof.go.jp/english/ |
| Cabinet Office Japan | https://www.cao.go.jp/index-e.html |
| Statistics Bureau Japan | https://www.stat.go.jp/english/ |
| Japan market holidays | https://www.jpx.co.jp/english/corporate/about-jpx/calendar/ |
| CME FX futures context | https://www.cmegroup.com/markets/fx.html |

Use Reuters ou AP para noticia em tempo real e geopolitica, preferencialmente com corroboracao oficial.

## 3. Agenda que merece verificacao

Verifique horario, consenso, realizado, revisao e fonte para:

- FOMC, atas, discursos do Fed, CPI, PPI, PCE, payroll, jobless claims, ISM/PMI, retail sales, GDP e Treasury auctions;
- BoJ rate decision, Outlook Report, discursos do governador, CPI Japao/Tokyo CPI, GDP, Tankan, wages, unemployment, trade balance e industrial production;
- comentarios do MoF sobre cambio, niveis psicologicos e suspeita/confirmacao de intervencao;
- feriados EUA/Japao, rolagem diaria, Tokyo fix, London fix e NY data windows.

## 4. Calculos reproduziveis

Use valores do broker e informe unidades:

```text
mid = (bid + ask) / 2
spread_abs = ask - bid
spread_points = spread_abs / point_size
spread_atr_M1 = spread_abs / ATR_M1
spread_atr_M5 = spread_abs / ATR_M5
ATR_percent = ATR / mid * 100
distance_level_atr = abs(mid - level) / ATR_do_timeframe
ema_separation_atr = abs(EMA_rapida - EMA_lenta) / ATR
pip_value_proxy = 0.01 para cotacoes JPY com 3 casas, apenas se metadata confirmar escala
```

Nao divida por zero. Quando ATR, point size, range ou metadados forem invalidos, marque a metrica indisponivel.

## 5. Leitura Price Action por regime

- **Tendencia definida:** topos/fundos coerentes, EMAs alinhadas, pullbacks contidos e follow-through.
- **Range amplo:** extremos respeitados, falsas rupturas, retorno ao VWAP/valor.
- **Range estreito:** baixa vantagem ate rompimento com continuidade; cuidado antes de dados EUA/Japao.
- **Transicao:** rompimento da estrutura anterior, reteste/pullback e segunda tentativa.

## 6. Lentes metodologicas publicas

### Al Brooks

Use tendencia versus trading range, always-in, canal, follow-through, pullbacks e falhas.

- https://www.brookspriceaction.com/
- https://www.brookstradingcourse.com/wp-content/uploads/wpforo/attachments/19757/2509-Price-Action-Decision-Tree-graphic.pdf

### Alexandre Wolwacz (Stormer)

Use contexto, setup objetivo, risco, disciplina e repetibilidade.

- https://www.infomoney.com.br/mercados/stormer-vence-1a-edicao-do-premio-top-traders-infomoney/

### Fabricio Lorenz

Use forca, impulso/correcao, pontos relevantes, rompimento/pullback e indicadores como apoio.

- https://lorenzfabricio.com.br/epa-tp-c/

## 7. Confianca

Para confianca alta, exija dados integros, regime claro H1/H4, gatilho coerente M15/M5/M1, ausencia de evento iminente, spread compativel com ATR, risco de intervencao controlado e estrategia implementada compativel.
