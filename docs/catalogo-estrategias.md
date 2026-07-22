# Catálogo de estratégias do robô

**Data da revisão:** 16/07/2026  
**Escopo:** estratégias registradas no Worker .NET e acionadas pelo motor `StrategyEngine`, além da gestão de ordens (SL/TP, breakeven, trailing e saídas parciais) aplicada a todas elas.

> Este documento descreve o comportamento efetivamente implementado. A indicação de cenário ideal é uma classificação de regime de mercado, não uma promessa de rentabilidade. Toda estratégia precisa ser validada por ativo, sessão, spread, slippage e custos antes de qualquer uso fora de conta demo.

## 1. Onde as estratégias realmente executam

As estratégias não ficam no EA MQL5. O EA é a ponte de comunicação com o MetaTrader 5; a decisão de comprar, vender ou aguardar fica no Worker .NET.

O catálogo registra oito estratégias em [`Program.cs`](../src/Financial.Robot.Worker/Program.cs):

1. `CruzamentoEma`
2. `PriceActionSuporteResistencia`
3. `OpeningRangeBreakout`
4. `ReversaoRange`
5. `ScalperWinPullbackCurto`
6. `HydrusEmaChannelBreakout`
7. `MicroTendenciaPullbackEma`
8. `PriceActionBarByBar`

O motor tem estas características comuns:

- avalia a estratégia uma vez por novo candle fechado;
- usa como timeframe-base o timeframe do primeiro indicador configurado; sem indicadores, usa `M1`;
- a estratégia decide direção, motivo e, opcionalmente, um Stop Loss estrutural (`StopSugerido`); lote, TP, execução e gestão dinâmica (breakeven/trailing/parciais) são tratados depois pelo motor — ver seção 4A;
- uma entrada sem Stop Loss calculável é bloqueada;
- janela de horário, drawdown, limite de posições, cooldown e stops consecutivos são filtros posteriores ao sinal;
- os valores de distância podem ser configurados com o sufixo `...Preco` (ex: `toleranciaRompimentoPreco`). Para retrocompatibilidade, o sufixo `...Pontos` continua funcionando como fallback, mas trata-se de diferença direta absoluta de preço, sem conversão automática pelo tamanho do tick.

Consequência prática: a ordem dos indicadores no JSON, a unidade de preço do símbolo e a configuração de saída fazem parte do comportamento operacional, mesmo quando não aparecem na lógica de entrada.

## 2. Matriz de adequação por regime

| Estratégia | Natureza | Tendência nascendo | Tendência definida | Mercado lateral | Abertura volátil | Principal risco de regime |
|---|---|---:|---:|---:|---:|---|
| `CruzamentoEma` | Seguimento de tendência por cruzamento | Alta | Média | Baixa | Média | Cruzamentos falsos e alternados em congestão |
| `HydrusEmaChannelBreakout` | Rompimento de canal em torno de EMA longa | Alta | Alta | Baixa | Média | Canal fixo inadequado à volatilidade corrente |
| `OpeningRangeBreakout` | Momentum após rompimento da abertura | Média | Condicional | Baixa | Alta | Falso rompimento e retorno ao range |
| `PriceActionSuporteResistencia` com reteste | Rompimento e continuação em nível | Alta | Alta, em pullback | Média | Média | Nível desatualizado ou reteste mal identificado |
| `PriceActionSuporteResistencia` com rejeição | Reversão em nível | Baixa | Média, a favor da tendência maior | Alta | Baixa | Tentar antecipar reversão contra tendência forte |
| `ReversaoRange` | Reversão à média nos extremos | Baixa | Baixa | Alta | Baixa | RSI permanecer extremo durante rompimento real |
| `ScalperWinPullbackCurto` | Pullback curto a favor da tendência M5 | Média | Alta | Baixa | Média | Bloqueio atual de histórico e níveis intradiários vencidos |
| `MicroTendenciaPullbackEma` | Pullback direcional curto intra-day | Média | Alta | Baixa | Média | Mudança súbita de tendência cruzando a EMA Média antes do filtro atuar |
| `PriceActionBarByBar` | Leitura pura de candles: impulso + pullback + rompimento da barra de sinal | Alta | Alta, em pullback | Baixa | Média | Impulso mal classificado em mercado ruidoso; janelas de impulso/pullback mal calibradas para o ativo |

`Condicional` significa que o regime sozinho não basta. O horário, a formação do range e a qualidade do nível precisam estar corretos.

## 3. Guia rápido de escolha

- **Tendência começando após consolidação:** `CruzamentoEma` ou `HydrusEmaChannelBreakout`.
- **Tendência já definida, aguardando recuo:** `ScalperWinPullbackCurto`, depois de corrigido o problema de lookback.
- **Pequenas pernadas a favor da tendência intradiária (M1/M5):** `MicroTendenciaPullbackEma`.
- **Rompimento de nível técnico com confirmação:** `PriceActionSuporteResistencia` no modo reteste, depois de corrigida a leitura booleana.
- **Rejeição em suporte ou resistência:** `PriceActionSuporteResistencia` no modo rejeição.
- **Primeiros minutos da sessão com expansão direcional:** `OpeningRangeBreakout`.
- **Range maduro, extremos respeitados e baixa expansão:** `ReversaoRange`.
- **Leitura bar a bar sem indicadores, estilo Al Brooks (impulso/pullback/barra de sinal):** `PriceActionBarByBar`.
- **Mercado sem regime claro:** nenhuma das oito deve ser escolhida apenas para “estar no mercado”. Aguardar é uma decisão válida.

## 4. `CruzamentoEma`

**Fonte:** [`CruzamentoEma.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/CruzamentoEma.cs)

### Classificação

Seguimento de tendência com gatilho de mudança de direção. É mais adequada ao nascimento ou à retomada de uma tendência do que a uma tendência já muito esticada.

### O que o código exige

1. Pelo menos duas EMAs calculadas.
2. Cruzamento real entre o candle anterior e o atual:
   - compra quando a EMA rápida estava abaixo ou igual e passa para cima da lenta;
   - venda quando a EMA rápida estava acima ou igual e passa para baixo da lenta.
3. Se houver RSI:
   - compra somente com RSI entre 40 e 70;
   - venda somente com RSI entre 30 e 60.
4. Se houver `ForcaCesta`, o score pode contextualizar ou bloquear o sinal, conforme a configuração.

O código é *edge-triggered*: não compra repetidamente só porque a EMA rápida continua acima da lenta. Precisa ocorrer um novo cruzamento.

### Melhor cenário

- saída de uma consolidação com aumento de amplitude;
- retomada de tendência depois de um pullback suficientemente profundo para aproximar as médias;
- ativo líquido, com custo pequeno em relação ao ATR do timeframe;
- timeframe em que o ruído não domine o deslocamento médio. Na prática, `M5` tende a ser mais tolerante a custos do que `M1`, mas isso deve ser medido por ativo.

### Cenários a evitar

- lateralidade estreita, com EMAs se cruzando várias vezes;
- mercado errático após notícia, quando há cruzamento e reversão imediata;
- tendência já muito avançada: sem um novo cruzamento, não há entrada; se o cruzamento vier tarde, a relação risco/retorno pode piorar;
- spread, comissão ou slippage grandes comparados ao ATR e ao alvo.

### Limitações encontradas

- não há filtro de inclinação das médias, ADX, separação mínima entre EMAs ou expansão de ATR;
- os limites de RSI são fixos no código, não parâmetros;
- a ordenação das EMAs tenta ler `periodo` apenas como `int`. Valores vindos do JSON normalmente são `JsonElement`; por isso, a ordem declarada no JSON deve continuar sendo rápida primeiro e lenta depois;
- o parser privado de `ForcaCesta` não trata `JsonElement`. Limiares personalizados podem cair nos padrões 20/-20, e o bloqueio pode permanecer falso se o valor não vier como string nativa.

### Leitura profissional

Use como estratégia de transição de regime, não como detector universal de tendência. Uma tendência visualmente definida pode não produzir sinal porque o cruzamento ocorreu muito antes. Em congestão, o atraso natural das médias transforma ruído em sequência de sinais falsos.

## 5. `PriceActionSuporteResistencia`

**Fonte:** [`PriceActionSuporteResistencia.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/PriceActionSuporteResistencia.cs)

### Classificação

Estratégia híbrida. Pode operar continuação por rompimento e reteste ou reversão por rejeição. Os dois modos têm regimes ideais opostos e não devem ser analisados como se fossem a mesma estratégia.

### Modo A: rompimento com reteste

Pretendido quando `exigeRetesteConfirmado = true`.

O padrão procurado é:

1. primeiro candle da janela fecha além do nível mais a tolerância;
2. candles intermediários não ultrapassam o nível pelo lado errado além da tolerância;
3. candle final fecha acima da máxima do candle de rompimento, para compra, ou abaixo da mínima, para venda.

**Melhor cenário:** nível relevante, rompimento com aceitação fora da faixa, retorno controlado ao nível e retomada da direção. Funciona melhor em tendência nascendo ou continuação de tendência após pullback.

**Evitar:** rompimento sem volume/participação, nível testado muitas vezes, notícia com pavios grandes, mercado que volta a fechar dentro da faixa.

### Modo B: rejeição

Usado quando `exigeRetesteConfirmado = false`.

O último candle precisa tocar a tolerância do nível e apresentar:

- compra: pavio inferior maior que 1,5 vez o corpo e candle de alta;
- venda: pavio superior maior que 1,5 vez o corpo e candle de baixa.

**Melhor cenário:** range bem definido, primeiro ou segundo teste de um nível, falso rompimento ou pullback em nível alinhado à tendência maior.

**Evitar:** rejeição contra tendência forte, nível antigo, vários níveis muito próximos e candles de corpo quase nulo.

### Parâmetros principais

| Parâmetro | Função | Padrão no código |
|---|---|---:|
| `niveis` | Lista manual de `{ tipo, preco }` | Obrigatório |
| `toleranciaRompimentoPontos` | Margem ao redor do nível | 50 |
| `exigeRetesteConfirmado` | Seleciona reteste ou rejeição | `true` pretendido |
| `candlesConfirmacao` | Quantidade de candles intermediários | 2 |

### Limitações e Observações de Uso

1. **Validação de Níveis**: O rompimento e a rejeição agora respeitam o tipo do nível. Um nível `suporte` só gera sinal de compra por rejeição ou venda por rompimento, e um nível `resistencia` só gera sinal oposto. Níveis sem o tipo preenchido ou genéricos (`nivel`) operam nos dois sentidos.
2. Os níveis são manuais e não expiram automaticamente.
3. Não há filtro interno de spread, tendência, volume, ATR ou timeframe maior.
4. O Stop Loss não é colocado tecnicamente além do nível pela estratégia. O motor usa a configuração genérica de `Saida`.

### Leitura profissional e Medição Operacional

Trate os dois modos como estratégias separadas na análise de performance. **Para realizar a medição operacional isolada, o usuário deve criar dois blocos separados de estratégia no seu JSON de configuração**, um com `exigeRetesteConfirmado: true` e `magicNumber` específico, e outro com `exigeRetesteConfirmado: false` e outro `magicNumber`. Misturar rompimento e rejeição sob o mesmo identificador impede descobrir qual comportamento gerou o resultado.

## 6. `OpeningRangeBreakout`

**Fonte:** [`OpeningRangeBreakout.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/OpeningRangeBreakout.cs)

### Classificação

Momentum intradiário baseado no rompimento da máxima ou mínima da faixa de abertura.

### O que o código exige

1. `janelaFormacaoRange`, por exemplo `09:00-09:30`.
2. `janelaOperacao`, por exemplo `09:30-10:30`.
3. Pelo menos um candle do dia dentro da janela de formação.
4. Durante a janela de operação:
   - compra se o `Ask` estiver acima da máxima do range;
   - vende se o `Bid` estiver abaixo da mínima do range.

### Melhor cenário

- abertura com aumento real de volume e volatilidade;
- range inicial relativamente compacto, seguido de aceitação fora da faixa;
- dia direcional com catalisador conhecido e participação institucional;
- rompimento alinhado ao contexto do timeframe maior ou ao gap da sessão.

### Cenários a evitar

- abertura sem participação, com preço alternando os dois lados do range;
- range inicial excepcionalmente largo, que deixa pouco espaço para o alvo;
- rompimento causado apenas pelo spread ou por um único pavio;
- sessão já avançada ou após retorno completo para dentro do range.

### Limitações encontradas

- é um gatilho por nível, sem exigir fechamento fora do range, buffer, reteste, volume ou volatilidade mínima;
- pode sinalizar novamente em candles posteriores enquanto o preço continuar fora da faixa;
- não há limite interno de uma operação por sessão;
- usa `DateTime.UtcNow` para o dia e o horário. As janelas precisam estar alinhadas ao timestamp dos candles e ao fuso efetivo da sessão;
- o Stop Loss no lado oposto do range e o alvo proporcional à amplitude, previstos conceitualmente para ORB, não estão implementados. A saída é genérica.
- *Resolvido:* O lookback foi corrigido; a estratégia informa o histórico correto ao motor (no mínimo toda a duração desde a janela de formação).

### Leitura profissional

O ORB atual é uma versão mínima e agressiva. Antes de uso confiável, deveria exigir histórico suficiente para reconstrução diária, confirmação de rompimento e controle de uma entrada por direção ou por sessão.

## 7. `ReversaoRange`

**Fonte:** [`ReversaoRange.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/ReversaoRange.cs)

### Classificação

Reversão à média em extremos de uma faixa previamente mapeada.

### O que o código exige

- níveis manuais tipados como `suporte` e `resistencia`;
- RSI calculado;
- compra perto do suporte mais próximo com RSI menor ou igual ao limite de sobrevenda;
- venda perto da resistência mais próxima com RSI maior ou igual ao limite de sobrecompra.

| Parâmetro | Função | Padrão no código |
|---|---|---:|
| `rsiSobrevendaMaximo` | Limite para compra | 30 |
| `rsiSobrecompraMinimo` | Limite para venda | 70 |
| `distanciaMaximaDoNivelPontos` | Proximidade máxima do nível | 100 |

### Melhor cenário

- range maduro, com ao menos dois testes reconhecíveis dos extremos;
- inclinação pequena das médias no timeframe de contexto;
- ATR estável ou contraindo;
- ausência de catalisador imediato;
- uso de janela horária de menor direcionalidade, quando isso for confirmado pelos dados do ativo.

### Cenários a evitar

- tendência forte ou início de expansão;
- rompimento do range com fechamento e aceitação fora da faixa;
- RSI extremo causado por notícia ou deslocamento direcional persistente;
- suporte e resistência muito próximos em relação a spread, stop e comissão.

### Limitações encontradas

- RSI extremo não é confirmação de reversão; o código não exige candle de rejeição nem retorno do RSI para dentro da faixa;
- o sinal é por nível, não por cruzamento. Pode reaparecer enquanto preço e RSI continuarem na condição;
- não verifica inclinação, ADX, ATR, largura mínima do range ou aceitação fora do nível;
- a janela de horário não está dentro da estratégia; depende de `JanelaHorarioPermitido` no motor;
- a saída não mira automaticamente o meio ou o lado oposto do range.

### Leitura profissional

É a estratégia mais claramente lateral do catálogo. Deve ser desligada assim que houver evidência de mudança de regime. Em tendência, “sobrecomprado” e “sobrevendido” podem persistir por muitos candles.

## 8. `ScalperWinPullbackCurto`

**Fonte:** [`ScalperWinPullbackCurto.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/ScalperWinPullbackCurto.cs)

### Classificação

Pullback curto a favor de uma tendência confirmada no M5, com gatilho de rejeição no M1. Apesar do nome e dos parâmetros em pontos, a classe não bloqueia outros símbolos; seu desenho, porém, é especializado para WIN/índice futuro.

### O que o código exige

Para venda:

1. candle M1 testa uma resistência manual;
2. forma candle baixista com pavio superior de pelo menos duas vezes o corpo;
3. fecha abaixo da EMA9 M1;
4. RSI M1 não pode estar acima de `rsiMaximoVenda`;
5. preço precisa estar abaixo da EMA9 e da EMA21 M5.

Para compra, aplica a lógica espelhada em suporte, com candle de rejeição comprador, fechamento acima da EMA9 M1, RSI acima do mínimo e preço acima das duas EMAs M5.

Filtros adicionais:

- spread máximo;
- distância mínima absoluta da VWAP diária, para evitar congestão próxima ao preço médio;
- pelo menos 105 candles no histórico-base.

### Melhor cenário

- tendência M5 clara, mas não parabólica;
- pullback ordenado até nível intradiário relevante;
- candle M1 rejeita o nível e volta para o lado da tendência;
- preço suficientemente afastado da VWAP para indicar deslocamento, sem estar excessivamente esticado;
- boa liquidez e spread estável.

### Cenários a evitar

- preço cruzando repetidamente a VWAP e as EMAs M5;
- tendência explosiva sem pullback, em que o candle de rejeição chega tarde;
- níveis do pregão anterior não recalibrados;
- primeiros minutos com spread e volatilidade erráticos;
- lateralidade larga em que o preço fica acima/abaixo das EMAs por pouco tempo.

### Observações de Histórico

*Resolvido:* No fluxo normal do motor, a estratégia agora informa o limite de 105 candles necessários através da sobrescrita de `ObterLookbackNecessario`, corrigindo o bloqueio funcional.

### Outras limitações

- o filtro M5 exige apenas preço acima ou abaixo das duas EMAs; não exige EMA9 acima da EMA21 para compra nem abaixo para venda;
- a distância da VWAP é absoluta. Não exige venda abaixo da VWAP ou compra acima dela;
- não há filtro de volume no candle de rejeição;
- níveis são manuais e intradiários;
- o lookback precisa ser corrigido antes de avaliar qualidade de sinal ou desempenho.

### Leitura profissional

A arquitetura do setup é coerente para pullback em tendência, mas hoje a discussão de rentabilidade é prematura: primeiro é necessário garantir que a estratégia consiga sair do estado de histórico insuficiente no motor real.

## 9. `HydrusEmaChannelBreakout`

**Fonte:** [`HydrusEmaChannelBreakout.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/HydrusEmaChannelBreakout.cs)

### Classificação

Rompimento direcional de canal. Compara uma média curta do preço mediano com bandas fixas ao redor de uma EMA longa.

### O que o código exige

1. Calcula EMA longa do fechamento, padrão 156.
2. Calcula média curta, padrão SMMA(5), sobre preço mediano `(máxima + mínima) / 2`.
3. Cria bandas `EMA longa + canal` e `EMA longa - canal`.
4. Compra quando a média curta cruza a banda superior para cima.
5. Vende quando a média curta cruza a banda inferior para baixo.
6. Bloqueia spread acima do limite e entradas em que o preço já se afastou demais da banda.
7. Opcionalmente confirma o preço acima/abaixo de uma EMA em timeframe maior.

| Parâmetro | Função | Padrão no código |
|---|---|---:|
| `emaPeriodo` | EMA central longa | 156 |
| `mediaCurtaPeriodo` | Média do gatilho | 5 |
| `mediaCurtaTipo` | `SMMA`, `SMA` ou `EMA` | `SMMA` |
| `mediaCurtaPreco` | `Median` ou fechamento | `Median` |
| `canalPontos` | Distância fixa das bandas | 170 |
| `spreadMaximoPontos` | Spread máximo | 10 |
| `distanciaMaximaAposRompimentoPontos` | Evita perseguir preço distante | 80 |
| `usarConfirmacaoTimeframeMaior` | Ativa confirmação superior | `false` |
| `emaConfirmacaoPeriodo` | EMA do timeframe maior | 21 |

### Melhor cenário

- compressão seguida de expansão de volatilidade;
- tendência nascendo ou retomando força;
- preço permanecendo do mesmo lado da EMA do timeframe maior;
- ativo em que o canal fixo foi calibrado para a volatilidade e a escala de preço atuais;
- rompimento próximo à banda, sem entrada atrasada.

### Cenários a evitar

- lateralidade com a média curta atravessando as bandas repetidamente;
- volatilidade muito baixa, quando o canal fica largo demais e não há sinal;
- volatilidade muito alta, quando o canal fica estreito demais e gera ruído;
- gap ou notícia que coloca o preço muito além da banda;
- mudança de contrato ou escala de preço sem recalibração do canal.

### Limitações encontradas

- canal, spread e distância máxima usam unidades absolutas de preço, apesar do sufixo `Pontos`;
- o canal não se adapta a ATR ou desvio-padrão;
- a confirmação superior verifica preço versus EMA, mas não a inclinação da EMA nem o alinhamento de duas médias;
- não há confirmação de volume ou fechamento além da banda; o cruzamento é da média curta;
- timeframes maiores podem exigir milhares de candles M1, elevando custo de carregamento. A estratégia ao menos informa esse lookback ao motor corretamente.

### Leitura profissional

É a estratégia mais seletiva de rompimento do catálogo, pois exige cruzamento de uma banda e limita perseguição do preço. Sua robustez depende diretamente da calibração do canal ao ativo e ao regime de volatilidade.

## 10. `MicroTendenciaPullbackEma`

**Fonte:** [`MicroTendenciaPullbackEma.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/MicroTendenciaPullbackEma.cs)

### Classificação

Seguimento de micro tendências e pullbacks curtos intradiários. Captura recuos a favor de uma tendência confirmada por três EMAs e filtros de volatilidade (ATR), momentum (RSI) e volume/contexto (VWAP).

### O que o código exige

Para compra:
1. Tendência de alta nas 3 EMAs: `Rapida > Media > Lenta`.
2. As EMAs devem estar inclinadas para cima (valor atual maior que `candlesInclinacao` períodos atrás).
3. O preço atual (Bid) deve estar acima da EMA Lenta.
4. Ocorre um pullback onde a Mínima do candle atinge a região entre a EMA Rápida e a Lenta (dentro da `toleranciaPullbackPontos`).
5. Exige que o candle atual feche além de um deslocamento mínimo (`rompimentoMinimoPontos`) em relação à EMA Rápida ou à Máxima do candle anterior.
6. Filtros opcionais de VWAP (Bid > VWAP) e RSI (RSI < `rsiMaximoCompra`).
7. ATR atual deve ser maior que `atrMinimoPontos` (mercado não está morto), e o candle não pode exceder `candleMaxAtrMultiplo` vezes o ATR (rejeita candles absurdos que saturam o indicador).
8. Espaçamento coerente do Spread (`spreadMaximoPontos`).

Para venda, aplica-se a lógica invertida (tendência de baixa nas 3 EMAs, pullback nas máximas, rompimento abaixo da Mínima, Ask < VWAP).

### Melhor cenário

- Dias direcionais em ziguezague no M1 ou M5;
- Preço respeitando o alinhamento e distanciamento saudável das médias (não estão emboladas);
- Ocorrem pullbacks profundos que não quebram o alinhamento da EMA Média contra a Lenta;
- O candle de sinal retoma e rompe a máxima com volume e deslocamento real, demonstrando força da perna.

### Cenários a evitar

- Lateralidades estreitas, em que o preço corta as médias para cima e para baixo continuamente;
- O pullbak é tão intenso que o preço perde suporte da VWAP ou altera o viés intradiário;
- Momentos onde o ATR desaba, não tendo volatilidade para sustentar um descolamento após a entrada.

### Limitações e Observações de Uso

- O uso do `ParametroParser` exige que propriedades de preço/pontos sigam as convenções de tipo via fallback de nome.
- A restrição a candles gigantes via ATR atua em gaps também, prevenindo entradas erráticas se a primeira barra do dia varrer a faixa.
- Necessita de histórico condizente, lidando através do método `ObterLookbackNecessario(EstrategiaConfig config)`, assegurando que a inclinação e a EMA lenta se calcifiquem.

### Leitura profissional e Medição Operacional

Trata-se de uma estratégia sólida de acompanhamento. Requer calibração fina nos filtros (`rompimentoMinimoPontos` e `toleranciaPullbackPontos`) a depender do tempo gráfico.

## 11. `PriceActionBarByBar`

**Fonte:** [`PriceActionBarByBar.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/PriceActionBarByBar.cs)

### Classificação

Leitura pura de Price Action, sem indicadores, inspirada no método de Al Brooks (*Reading Price Charts Bar by Bar* e a trilogia *Trading Price Action*): impulso de tendência (trend bars) seguido de pullback controlado e entrada na confirmação de rompimento da barra de sinal.

### O que o código exige

1. Classifica cada candle como *trend bar* se o corpo ocupar ao menos `corpoMinimoFracaoRange` (padrão 55%) do range total.
2. Varre janelas de `candlesImpulsoMinimo`–`candlesImpulsoMax` candles em busca de um impulso: maioria de trend bars na mesma direção.
3. **Filtro de climax:** se alguma barra do impulso tiver range maior que `climaxMultiploRange` (padrão 2,5×) vezes o range médio recente (`lookbackMedioRange`), bloqueia a entrada — evita comprar exaustão/blow-off.
4. Exige um pullback de `candlesPullbackMinimo`–`candlesPullbackMax` candles que não devolva mais que `pullbackMaximoPercentual` (padrão 70%) da amplitude do impulso.
5. A última barra do pullback é a barra de sinal; a entrada só ocorre quando o candle atual **fecha** além da máxima (compra) ou mínima (venda) da barra de sinal — equivalente ao modo "candle fechado" de confirmação.
6. Sugere um Stop Loss estrutural (`StopSugerido`) no extremo oposto da barra de sinal ± `bufferStopPreco` (padrão 5 pontos) — ver seção 12 sobre como o motor usa esse valor.

| Parâmetro | Função | Padrão no código |
|---|---|---:|
| `corpoMinimoFracaoRange` | Fração mínima do range que deve ser corpo para contar como trend bar | 0.55 |
| `candlesImpulsoMinimo` / `candlesImpulsoMax` | Faixa de tamanho do impulso avaliado | 2 / 6 |
| `candlesPullbackMinimo` / `candlesPullbackMax` | Faixa de tamanho do pullback avaliado | 1 / 5 |
| `pullbackMaximoPercentual` | Retração máxima aceitável do pullback sobre o impulso | 0.7 |
| `lookbackMedioRange` | Janela para calcular o range médio (referência de climax) | 20 |
| `climaxMultiploRange` | Múltiplo do range médio que caracteriza climax/exaustão | 2.5 |
| `bufferStopPreco` | Distância extra além da barra de sinal para o Stop Loss estrutural | 5.0 |

### Melhor cenário

- tendência com impulsos limpos (poucos dojis) e pullbacks rasos, sem climax no meio do impulso;
- ativo e timeframe onde o range médio recente é uma referência estável de volatilidade "normal";
- mercado com viés direcional claro (Al Brooks: "always in").

### Cenários a evitar

- lateralidade ruidosa, onde qualquer sequência de 2-3 candles pode ser lida como "impulso" por acaso;
- abertura de pregão com o primeiro candle do dia caracterizando climax por gap (variação real, não ruído — o filtro deve bloquear corretamente, mas vale checar em backtest);
- ativos com poucos ticks por candle, onde corpo/range fica instável e o critério de trend bar oscila.

### Limitações encontradas

- não distingue tendência maior do impulso local: um impulso de 2-3 candles válido tecnicamente pode ir contra o viés do timeframe superior;
- o filtro de climax só olha as barras do impulso, não a barra de confirmação do rompimento nem as barras do pullback;
- sem filtro de spread, horário ou volume próprios — depende inteiramente dos filtros genéricos do motor (`JanelaHorarioPermitido`, `GestaoDeRisco`).

### Leitura profissional

É a única estratégia do catálogo que não depende de nenhum indicador — só lê estrutura de candle. Isso a torna mais transparente para auditoria manual (fácil de conferir no gráfico), mas também mais sensível a ruído em ativos/timeframes de baixa liquidez. Validar cuidadosamente `corpoMinimoFracaoRange` e `climaxMultiploRange` por ativo antes de qualquer uso real.

## 12. Gestão de ordens: SL, TP, breakeven, trailing e saídas parciais

A gestão de ordens é **desacoplada da entrada** e comum a todas as oito estratégias — nenhuma delas fecha posições sozinha; todas usam o mesmo `SaidaConfig` no JSON e os mesmos componentes do motor.

### Onde cada peça mora

| Peça | Componente | Quando roda |
|---|---|---|
| Stop Loss e Take Profit de abertura | [`StrategyEngine.CalcularSlTpAsync`](../src/Financial.Robot.Worker/Strategy/StrategyEngine.cs) | No momento em que a entrada é executada |
| Breakeven e trailing stop | [`GerenciadorPosicoesAbertasService`](../src/Financial.Robot.Worker/Execution/GerenciadorPosicoesAbertasService.cs) | A cada 10s, para toda posição aberta |
| Saídas parciais (scale-out) | [`GerenciadorPosicoesAbertasService`](../src/Financial.Robot.Worker/Execution/GerenciadorPosicoesAbertasService.cs) + [`CalculoSaidaParcial`](../src/Financial.Robot.Worker/Execution/CalculoSaidaParcial.cs) | A cada 10s, antes do breakeven/trailing |

### Stop Loss: genérico por config ou estrutural por estratégia

A interface `IEstrategiaEntrada.Avaliar` retorna um `ResultadoDecisao` que pode carregar um `StopSugerido` opcional — um nível de preço estrutural (ex: a mínima/máxima da barra de sinal) que tem prioridade sobre o SL genérico do `SaidaConfig`. Hoje só `PriceActionBarByBar` usa esse recurso; as outras sete continuam com SL via `StopLossPips` ou `StopLossAtrMultiplo`. Quando há `StopSugerido`, o Take Profit é calculado como múltiplo desse risco real (via `TakeProfitAtrMultiplo`/`TakeProfitPips` da config, ou `2×` o risco se nada for configurado) — permite um TP "mais longo" por segurança sem inventar alvo técnico. Se `TakeProfitAtrMultiplo` estiver configurado mas nenhum indicador ATR estiver disponível para a estratégia, o motor loga um aviso e cai para `2×` o risco.

### Breakeven e trailing (genéricos, por `SaidaConfig`)

- `BreakevenGatilhoAtrMultiplo` + `BreakevenBufferPips`: quando o lucro atinge o gatilho (em múltiplos de ATR), move o SL para a entrada + buffer.
- `TrailingStopPips` (prioridade) ou `TrailingStopAtrMultiplo`: acompanha o preço a uma distância fixa, nunca move o SL para pior.
- Ambos nunca fecham posição — só ajustam o SL via `ModificarPosicaoAsync`.

### Saídas parciais (scale-out)

Novo em `SaidaConfig`: `SaidasParciais` (lista ordenada de `{ percentualVolume, distanciaPreco }`) e `BreakEvenAposParcial` (bool).

- Cada alvo é consumido em ordem; no máximo uma parcial por posição por ciclo de 10s.
- O volume de cada parcial é uma fração do volume **original** da posição, reconstruído a partir do volume atual e da soma dos percentuais já consumidos — normalizado ao `step`/`min`/`max` de volume do símbolo.
- Com `BreakEvenAposParcial: true`, o SL avança a cada parcial: para a entrada após a 1ª, e para o preço da parcial anterior a partir da 2ª — mesma regra do "Break Even Financeiro" descrito no manual de referência da estratégia Price Action.
- O progresso de parciais por ticket é mantido em memória, rastreado por (terminal, símbolo, magic number); um passo de reconciliação por ciclo confirma contra o broker antes de descartar progresso de um ticket que não apareceu no sweep normal, evitando re-disparo de uma parcial já executada.
- **Fechamento total permanece fora do escopo desta camada**: se o alvo cobrir o volume inteiro restante (ou o resíduo ficar abaixo do lote mínimo do símbolo), a posição é fechada por completo nessa "parcial".

Exemplo de bloco `saida` com parciais:

```json
"saida": {
  "stopLossPips": 200,
  "saidasParciais": [
    { "percentualVolume": 0.5, "distanciaPreco": 200 },
    { "percentualVolume": 0.5, "distanciaPreco": 400 }
  ],
  "breakEvenAposParcial": true,
  "trailingStopAtrMultiplo": 1.5
}
```

### Limitações conhecidas

- não há suporte a rollover de contrato (ex: `WINQ26` → `WINV26`) com progresso de parcial pendente: o gerenciador é chaveado pelo símbolo atual da config, então uma troca de símbolo em pleno andamento pode fazer o progresso de parcial ser reiniciado — comportamento consistente com o resto do gerenciador (breakeven/trailing têm a mesma limitação), não algo exclusivo das parciais;
- a reconstrução do volume original nas parciais assume que todas as parciais anteriores foram executadas exatamente como configurado; drift de arredondamento do broker ao longo de várias parciais pode acumular pequenas diferenças.

## 13. Prioridades técnicas e Pendências

| Prioridade | Ação | Status |
|---|---|---|
| Bloqueante | Implementar `ObterLookbackNecessario` no `ScalperWinPullbackCurto` | **Resolvido** (na rodada 1) |
| Bloqueante | Criar parser booleano e corrigir `exigeRetesteConfirmado` | **Resolvido** (na rodada 1) |
| Alta | Garantir lookback diário no ORB, inclusive após reinício | **Resolvido** (na rodada 2) |
| Alta | Criar testes integrados entre estratégia e `StrategyEngine` | **Resolvido** (na rodada 2) |
| Alta | Separar magic numbers dos dois modos de Price Action | **Resolvido** (via doc de config) |
| Média | Validar `tipo` do nível no Price Action | **Resolvido** (na rodada 2) |
| Média | Tornar unidades de distância explícitas por símbolo | **Resolvido** (sufixo `...Preco` adicionado) |
| Média | Adicionar filtros de regime parametrizáveis | Pendente |

Hoje existem testes dedicados para `HydrusEmaChannelBreakout`, `ScalperWinPullbackCurto`, `OpeningRangeBreakout`, `PriceActionSuporteResistencia`, `MicroTendenciaPullbackEma` e `PriceActionBarByBar`. `CruzamentoEma` e `ReversaoRange` não têm arquivos de teste específicos no projeto. A gestão de ordens (breakeven/trailing/parciais) tem cobertura via [`CalculoSaidaParcialTests.cs`](../tests/Financial.Robot.Worker.Tests/Execution/CalculoSaidaParcialTests.cs), mas o `GerenciadorPosicoesAbertasService` em si não tem teste de integração (depende de `ConfigWatcherService`, difícil de instanciar em teste isolado — por isso a lógica de cálculo foi extraída para `CalculoSaidaParcial`, testável separadamente).

## 14. Protocolo profissional de validação

Antes de promover qualquer estratégia:

1. Backtest por símbolo e timeframe com spread variável, comissão, slippage e horário real.
2. Separação dos resultados por regime: tendência, range, abertura e alta volatilidade.
3. Amostra fora do período de calibração e validação walk-forward.
4. Métricas por `magicNumber`: resultado líquido, fator de lucro, expectativa por trade, drawdown, MAE, MFE, sequência de perdas e trades por hora.
5. Teste de sensibilidade dos parâmetros. Pequenas mudanças não deveriam destruir completamente o resultado.
6. Execução em demo para validar timestamps, stops mínimos, volume, reconexão e comportamento após reinício.
7. Revisão de correlação entre estratégias simultâneas. Duas classes diferentes podem estar assumindo o mesmo risco direcional.

Nenhuma conclusão deve ser tomada apenas por taxa de acerto. Resultado líquido após custos, drawdown e estabilidade entre regimes são mais importantes.

## 15. Evidência externa e limites de inferência

- A literatura de *time-series momentum* encontra persistência de tendência em vários futuros, mas em horizontes muito maiores que M1/M5; serve como fundamento conceitual, não como prova da `CruzamentoEma` intradiária: [Moskowitz, Ooi e Pedersen, Time Series Momentum](https://pages.stern.nyu.edu/~lpederse/papers/TimeSeriesMomentum.pdf).
- Regras de média móvel e rompimento de faixa têm evidência histórica, e bandas ao redor das médias foram estudadas para reduzir sinais de “whiplash”; os resultados não dispensam teste atual por ativo e custo: [Brock, Lakonishok e LeBaron, Simple Technical Trading Rules](https://bashtage.github.io/kevinsheppard.com/files/teaching/mfe/advanced-econometrics/Brock_Lakonishok_LeBaron.pdf).
- Suportes e resistências mostraram capacidade de antecipar interrupções intradiárias em câmbio, mas o poder variou entre pares e fornecedores de níveis: [Federal Reserve Bank of New York, Support for Resistance](https://www.newyorkfed.org/medialibrary/media/research/epr/00v06n2/0007osle.pdf).
- Há pesquisa favorável a ORB em futuros de petróleo, mas esse resultado é específico à amostra e não pode ser transferido automaticamente para índices, Forex ou outro horário: [Finance Research Letters, Assessing the Profitability of Intraday Opening Range Breakout Strategies](https://www.sciencedirect.com/science/article/abs/pii/S1544612312000438).
- A constatação do booleano usa o comportamento documentado do .NET: `JsonElement.ToString()` retorna `Boolean.TrueString` para JSON verdadeiro: [Microsoft Learn, JsonElement.ToString](https://learn.microsoft.com/pt-br/dotnet/api/system.text.json.jsonelement.tostring).
- `PriceActionBarByBar` traduz mecanicamente conceitos descritos por Al Brooks (trend bar, pullback, barra de sinal, reversão climática) em regras verificáveis por candle; a leitura de Brooks é qualitativa e discricionária por natureza — a versão em código é uma aproximação e precisa de validação estatística própria, não herda a "credibilidade" da fonte.

## 16. Fontes internas revisadas

- [`Program.cs`](../src/Financial.Robot.Worker/Program.cs)
- [`StrategyEngine.cs`](../src/Financial.Robot.Worker/Strategy/StrategyEngine.cs)
- [`IEstrategiaEntrada.cs`](../src/Financial.Robot.Worker/Strategy/IEstrategiaEntrada.cs)
- [`ParametroParser.cs`](../src/Financial.Robot.Worker/Indicators/ParametroParser.cs)
- [`VwapIndicador.cs`](../src/Financial.Robot.Worker/Indicators/VwapIndicador.cs)
- [`CruzamentoEma.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/CruzamentoEma.cs)
- [`PriceActionSuporteResistencia.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/PriceActionSuporteResistencia.cs)
- [`OpeningRangeBreakout.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/OpeningRangeBreakout.cs)
- [`ReversaoRange.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/ReversaoRange.cs)
- [`ScalperWinPullbackCurto.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/ScalperWinPullbackCurto.cs)
- [`HydrusEmaChannelBreakout.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/HydrusEmaChannelBreakout.cs)
- [`MicroTendenciaPullbackEma.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/MicroTendenciaPullbackEma.cs)
- [`PriceActionBarByBar.cs`](../src/Financial.Robot.Worker/Strategy/Estrategias/PriceActionBarByBar.cs)
- [`ResultadoDecisao.cs`](../src/Financial.Robot.Domain/ValueObjects/ResultadoDecisao.cs)
- [`SaidaConfig.cs`](../src/Financial.Robot.Domain/ValueObjects/SaidaConfig.cs)
- [`SaidaParcialConfig.cs`](../src/Financial.Robot.Domain/ValueObjects/SaidaParcialConfig.cs)
- [`GerenciadorPosicoesAbertasService.cs`](../src/Financial.Robot.Worker/Execution/GerenciadorPosicoesAbertasService.cs)
- [`CalculoSaidaParcial.cs`](../src/Financial.Robot.Worker/Execution/CalculoSaidaParcial.cs)
- [`IGatewayMt5.cs`](../src/Financial.Robot.Domain/Interfaces/IGatewayMt5.cs)
- [`HydrusEmaChannelBreakoutTests.cs`](../tests/Financial.Robot.Worker.Tests/Strategy/HydrusEmaChannelBreakoutTests.cs)
- [`ScalperWinPullbackCurtoTests.cs`](../tests/Financial.Robot.Worker.Tests/Strategy/ScalperWinPullbackCurtoTests.cs)
- [`MicroTendenciaPullbackEmaTests.cs`](../tests/Financial.Robot.Worker.Tests/Strategy/MicroTendenciaPullbackEmaTests.cs)
- [`PriceActionBarByBarTests.cs`](../tests/Financial.Robot.Worker.Tests/Strategy/PriceActionBarByBarTests.cs)
- [`CalculoSaidaParcialTests.cs`](../tests/Financial.Robot.Worker.Tests/Execution/CalculoSaidaParcialTests.cs)

