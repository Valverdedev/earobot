# Spec Fase 5 — Estratégias plugáveis operando ao vivo (conta demo), com comparação de performance real

## Motivação

O sistema hoje tem uma única estratégia hardcoded no `StrategyEngine.AvaliarDecisao`: cruzamento EMA9/EMA21 com filtro opcional de RSI. Isso foi suficiente para validar que o mecanismo de execução funciona ponta a ponta (fase de teste com cripto), mas é raso demais como estratégia real para o `Bra50Aug26` — cruzamento de médias sozinho é atrasado por natureza e gera muito sinal falso em mercado lateral (ex.: o período de almoço, já identificado como fraco).

Decisão explícita do usuário: **não** vamos comparar estratégias em modo sombra/log — a conta de execução do Bra50 é demo, então cada estratégia deve **operar de verdade** (ordem real na conta demo), e a comparação de qual é mais eficiente vem do histórico real de negociações (`get_deals`), não de simulação.

Isso muda o desenho: precisamos rodar **múltiplas estratégias simultaneamente no mesmo símbolo**, cada uma enviando suas próprias ordens reais (na demo), mas identificáveis e isoláveis umas das outras — para que dê para agrupar o resultado por estratégia depois e decidir qual promover para conta real.

## 1. Nova interface: `IEstrategiaEntrada`

Generaliza a lógica hoje presa dentro de `StrategyEngine.AvaliarDecisao` para um catálogo de estratégias plugáveis, no mesmo espírito do catálogo de indicadores:

```csharp
public interface IEstrategiaEntrada
{
    string Nome { get; }

    /// <summary>
    /// Avalia se há sinal de entrada com base nos candles, indicadores já calculados
    /// (série única e multi-fonte) e no tick atual. Não decide volume nem SL/TP —
    /// só a direção (compra/venda/aguardar) e o racional.
    /// </summary>
    ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorMultiFonteConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        IDictionary<string, object> parametros);
}
```

`ResultadoDecisao` reaproveita o tipo já existente (`Comprar`/`Vender`/`Aguardar` + motivo).

## 2. Múltiplas instâncias de estratégia por símbolo — mudança estrutural no `StrategyEngine`/Factory

Hoje o `StrategyEngineFactory` cria **um** `StrategyEngine` por `SymbolConfig` (um por símbolo). Passa a criar **um `StrategyEngine` por instância de estratégia configurada** para aquele símbolo — todas compartilhando o mesmo feed de candles/ticks (não precisa duplicar a assinatura no Market Watch), mas cada uma com sua própria lógica de decisão, seu próprio identificador (`magicNumber`) e sua própria gestão de risco independente.

Schema de config novo — em vez de um `SymbolConfig` monolítico com `indicadores` fixos, o `Bra50Aug26.config.json` passa a ter uma lista de `estrategias`:

```json
{
  "symbol": "Bra50Aug26",
  "terminalId": "corretora-b",
  "operar": true,
  "estrategias": [
    {
      "id": "cruzamento-ema",
      "nome": "CruzamentoEma",
      "magicNumber": 101,
      "ativa": true,
      "comprar": true,
      "vender": true,
      "indicadores": [
        { "nome": "EMA", "timeframe": "M1", "parametros": { "periodo": 9 } },
        { "nome": "EMA", "timeframe": "M1", "parametros": { "periodo": 21 } }
      ],
      "saida": { "unidadeDistancia": "precoAbsoluto", "stopLossPips": 300, "takeProfitPips": 450, "slMinimoSobreSpread": 1.5 },
      "gestaoDeRisco": { "modoLote": "loteFixo", "loteFixo": 1, "maxOperacoesSimultaneas": 1, "drawdownDiarioMaximoPercent": 1.5 }
    },
    {
      "id": "price-action-sr",
      "nome": "PriceActionSuporteResistencia",
      "magicNumber": 102,
      "ativa": true,
      "comprar": true,
      "vender": true,
      "parametros": {
        "niveis": [ { "tipo": "suporte", "preco": 179500 }, { "tipo": "resistencia", "preco": 181800 } ],
        "toleranciaRompimentoPontos": 150,
        "exigeRetesteConfirmado": true,
        "candlesConfirmacao": 2
      },
      "saida": { "unidadeDistancia": "precoAbsoluto", "stopLossPips": 250, "takeProfitPips": 500, "slMinimoSobreSpread": 1.5 },
      "gestaoDeRisco": { "modoLote": "loteFixo", "loteFixo": 1, "maxOperacoesSimultaneas": 1, "drawdownDiarioMaximoPercent": 1.5 }
    },
    {
      "id": "opening-range-breakout",
      "nome": "OpeningRangeBreakout",
      "magicNumber": 103,
      "ativa": true,
      "comprar": true,
      "vender": true,
      "parametros": {
        "janelaFormacaoRange": "09:00-09:30",
        "janelaOperacao": "09:30-10:30",
        "confirmacaoVolumeMinimo": null
      },
      "saida": { "unidadeDistancia": "precoAbsoluto", "stopLossPips": 200, "takeProfitPips": 400, "slMinimoSobreSpread": 1.5 },
      "gestaoDeRisco": { "modoLote": "loteFixo", "loteFixo": 1, "maxOperacoesSimultaneas": 1, "drawdownDiarioMaximoPercent": 1.5 }
    },
    {
      "id": "reversao-range-almoco",
      "nome": "ReversaoRange",
      "magicNumber": 104,
      "ativa": true,
      "comprar": true,
      "vender": true,
      "janelaHorarioPermitido": { "inicio": "12:00", "fim": "14:00" },
      "indicadores": [ { "nome": "RSI", "timeframe": "M5", "parametros": { "periodo": 14 } } ],
      "parametros": {
        "niveis": [ { "tipo": "suporte", "preco": 179500 }, { "tipo": "resistencia", "preco": 181800 } ],
        "rsiSobrevendaMaximo": 30,
        "rsiSobrecompraMinimo": 70,
        "distanciaMaximaDoNivelPontos": 100
      },
      "saida": { "unidadeDistancia": "precoAbsoluto", "stopLossPips": 150, "takeProfitPips": 200, "slMinimoSobreSpread": 1.5 },
      "gestaoDeRisco": { "modoLote": "loteFixo", "loteFixo": 1, "maxOperacoesSimultaneas": 1, "drawdownDiarioMaximoPercent": 1.5 }
    }
  ],
  "indicadoresMultiFonte": [
    { "nome": "ForcaCesta", "parametros": { "...": "..." } }
  ]
}
```

Cada bloco de `estrategias[]` é essencialmente o que hoje é um `SymbolConfig` inteiro (saída, gestão de risco, indicadores), só que aninhado. O `StrategyEngineFactory` itera essa lista e sobe uma engine por item, todas compartilhando o `IMarketDataService`/feed de ticks do símbolo, mas decidindo e executando de forma independente.

**Retrocompatibilidade**: para os símbolos já existentes (BTCUSD, ADAUSD, etc.) que usam o schema antigo (um `SymbolConfig` só, sem `estrategias[]`), o loader deve tratar a ausência do campo como "uma única estratégia implícita chamada `CruzamentoEma` com `magicNumber` padrão (ex.: 1)" — não quebra nada do que já está rodando.

## 3. Magic number como identificador de estratégia — mudanças no Gateway/EA

Hoje `AbrirOrdemMercadoAsync` (em `IGatewayMt5`) já recebe um `comentario` (usado para popular o campo `comment` do MT5, visto no `get_deals` como `"financial.robot"`), mas **não** recebe/define um `magicNumber` explícito — o valor `magic=3` observado no histórico veio de um padrão fixo do lado MQL5/MtApi5, não de algo configurável pelo .NET hoje.

Mudança necessária:
1. `IGatewayMt5.AbrirOrdemMercadoAsync` passa a receber um parâmetro `long magicNumber` (e repassar para o `OrderSend` do MQL5/MtApi5 — verificar se a biblioteca MtApi5 expõe isso diretamente na chamada ou exige configuração por EA/instância).
2. `IServicoExecucao.AbrirPosicaoAsync` propaga esse `magicNumber` (vindo da config da estratégia) até o gateway.
3. `IGatewayMt5.ObterTicketsPosicoesAbertasAsync` passa a aceitar um filtro opcional por `magicNumber`, para que `RiskGuard.ValidarMaxOperacoes` conte só as posições **daquela estratégia**, não todas as posições do símbolo somadas (hoje contaria posições de todas as estratégias juntas, o que travaria umas às outras incorretamente).
4. Cada estratégia usa seu próprio `comentario` também (ex.: `"financial.robot|cruzamento-ema"`), como identificador redundante/legível em caso do magic number não estar disponível em algum ponto da cadeia.

**Isso é o ponto crítico da fase**: sem isolar por magic number, as 4 estratégias rodando juntas na mesma conta demo vão se contar mutuamente como "posições abertas" e travar umas às outras via `maxOperacoesSimultaneas`, além de tornar impossível separar o P&L de cada uma depois.

## 4. Risco agregado — proteção adicional necessária

Com múltiplas estratégias reais operando ao mesmo tempo no mesmo símbolo, o risco combinado da conta aumenta (mesmo sendo demo — o objetivo aqui também é validar que o robô se comporta corretamente sob essa carga, antes de pensar em conta real). Adicionar:

- `drawdownDiarioMaximoPercent` **por estratégia** (já no exemplo acima) — mas também um teto agregado por símbolo/conta no nível do `SymbolConfig` pai (ex.: `"drawdownDiarioMaximoAgregadoPercent": 5.0`), verificado antes de qualquer estratégia abrir posição, usando a equity real da conta (`ObterInfoContaAsync`), não a soma teórica dos limites individuais.
- Lote fixo pequeno por estratégia (ex.: 1 contrato cada) para a fase de comparação — múltiplas estratégias com lote grande simultâneo é desnecessariamente arriscado até saber qual funciona.

## 5. Estratégia detalhada: Price Action — Suporte/Resistência

A que você mais quer usar. Desenho:

- **Fonte dos níveis**: inicialmente manuais, vindos do relatório diário (`prompt_analise_bra50.md` já traz uma seção de análise técnica com suporte/resistência — só precisa extrair esses níveis para o bloco de calibração JSON, igual já fazemos com os pesos do `ForcaCesta`). Automatizar depois (ex.: máximas/mínimas de N candles) é possível, mas começar manual/assistido por IA é mais confiável e mais rápido de entregar.
- **Lógica de entrada — rompimento com reteste** (mais conservador, menos sinal falso que rompimento puro):
  1. Preço rompe o nível (fecha um candle além do nível + `toleranciaRompimentoPontos`).
  2. Preço retorna e testa o nível rompido (não fecha de volta do lado errado).
  3. Candle de confirmação na direção do rompimento (ex.: fecha acima da máxima do candle de reteste, para compra).
  4. Entra na abertura do candle seguinte à confirmação.
- **Lógica de entrada — rejeição no nível** (alternativa, sem esperar rompimento): preço se aproxima do nível, forma candle de rejeição (pavio longo do lado do nível, fechamento oposto ao pavio), entra contra o movimento que trouxe o preço até ali.
- SL: logo além do nível rompido/testado (não em múltiplo de ATR — o nível técnico já define a distância natural). TP: próximo nível técnico seguinte, ou múltiplo fixo da distância do SL (ex.: 2x) se não houver outro nível mapeado.
- `exigeRetesteConfirmado: false` deveria estar disponível como parâmetro para permitir testar a variante de rompimento direto (mais sinais, mais ruído) contra a de reteste (menos sinais, mais qualidade) — literalmente uma pergunta que a comparação de performance real deveria responder.

## 6. Estratégia detalhada: Opening Range Breakout

Pensada para os primeiros 30-60 min do futuro (9h-9h30 ou 9h-10h, parametrizável), quando o índice já opera mas as ações à vista ainda não abriram (ver achado da sessão anterior sobre o descompasso de horário — aqui o `ForcaCesta` não é usado, propositalmente, pois estaria defasado):

1. Durante `janelaFormacaoRange`, só observa — registra máxima e mínima do período, não opera.
2. Ao final da janela, define o range (máxima/mínima formadas).
3. Durante `janelaOperacao`, entra na direção do rompimento do range (compra se romper a máxima, vende se romper a mínima), com SL no lado oposto do range (ou um pouco além) e TP como múltiplo da amplitude do próprio range.
4. Fora de `janelaOperacao`, a estratégia fica inativa até o próximo pregão (reseta o range diariamente).

## 7. Estratégia detalhada: Reversão em Range (janela de almoço)

Já esboçada na conversa anterior, detalhando aqui: só ativa dentro de `janelaHorarioPermitido` (12h-14h), opera contra extremos dentro de um range já mapeado (mesmos níveis de suporte/resistência da estratégia de price action, reaproveitados), com filtro de RSI em extremo (sobrevenda perto do suporte → compra; sobrecompra perto da resistência → venda) e SL/TP mais curtos (a lógica é lateralização, não perseguir tendência).

## 8. Comparação de performance — via dados reais, não simulação

Como todas as estratégias operam de verdade na demo, a comparação vem de consultar `get_deals` (ou `get_deals(symbol="Bra50Aug26")`) e agrupar por `magic`/`comment`. Isso já é possível **hoje, sem nenhum código novo**, usando os tools MCP disponíveis nesta sessão — dá pra gerar um relatório comparativo periodicamente (diário/semanal) somando lucro líquido, número de trades, taxa de acerto e maior perda por magic number, e usar isso pra decidir qual estratégia manter/promover.

Sugestão: depois de alguns dias de dados reais acumulados, eu (ou um processo automatizado futuro) gero esse comparativo puxando `get_deals` e cruzando com o `magicNumber` de cada estratégia — não precisa esperar nenhuma feature nova de código para isso, só esperar as estratégias 1-4 estarem implementadas e rodando.

## Ordem de implementação sugerida

1. `IEstrategiaEntrada` + catálogo de estratégias (paralelo ao catálogo de indicadores).
2. Suporte a `magicNumber` de ponta a ponta (Gateway → ServicoExecucao → RiskGuard filtrando por magic).
3. Loader de config aceitando `estrategias[]` (com fallback retrocompatível para o schema antigo de símbolo único).
4. `StrategyEngineFactory` subindo N engines por símbolo quando houver `estrategias[]`.
5. Implementar `CruzamentoEma` (extrair do código atual, sem mudar comportamento) e `PriceActionSuporteResistencia` primeiro — são as duas mais imediatas/importantes.
6. Implementar `OpeningRangeBreakout` e `ReversaoRange` na sequência.
7. `Bra50Aug26.config.json` com as 4 estratégias, magic numbers 101-104, lote fixo pequeno, `drawdownDiarioMaximoAgregadoPercent` configurado.
8. Rodar em demo por um período, gerar o primeiro relatório comparativo via `get_deals` agrupado por magic.

## Critério de aceite

- [ ] 4 estratégias (`CruzamentoEma`, `PriceActionSuporteResistencia`, `OpeningRangeBreakout`, `ReversaoRange`) operam simultaneamente no `Bra50Aug26` em conta demo, cada uma com seu próprio `magicNumber`.
- [ ] `RiskGuard.ValidarMaxOperacoes` conta posições isoladas por magic number — uma estratégia não bloqueia a outra.
- [ ] `get_deals` mostra ordens de todas as 4 estratégias com `magic`/`comment` distintos, permitindo separar P&L por estratégia sem ambiguidade.
- [ ] Drawdown diário agregado (conta toda) é validado antes de qualquer nova entrada de qualquer estratégia, além do limite individual de cada uma.
- [ ] Config antigo de símbolo único (sem `estrategias[]`) continua funcionando sem alteração — retrocompatibilidade confirmada rodando um símbolo cripto já existente após a mudança.
