# Spec Fase 4 — Motor de Indicadores Personalizados (multi-fonte, parametrizável, calibrado por análise)

## Motivação

Hoje o sistema só suporta indicadores clássicos de série única (EMA, RSI, ATR, MACD via `IIndicador.Calcular(candles, parametros)`), sempre calculados sobre o próprio candle do símbolo/terminal que está sendo negociado (`StrategyEngine._candles`).

Com a conexão do terminal **Genial** (dedicado a dados, sem execução), passamos a ter acesso a séries de outros símbolos e de outro terminal — por exemplo, as 4 ações que mais pesam no Ibovespa (`VALE3`, `PETR4`, `ITUB4`, `BBDC4`) e o contrato contínuo `WIN$`/`WDO$` — que podem alimentar um **indicador composto de força/contexto** para o `Bra50Aug26`, negociado em outro terminal/corretora.

O objetivo desta fase é generalizar o motor de indicadores para suportar:
1. Indicadores **multi-fonte** (múltiplos símbolos, potencialmente de terminais MT5 diferentes) além dos indicadores de série única já existentes.
2. Indicadores **totalmente parametrizáveis** via config JSON (pesos, limiares, símbolos incluídos) — sem precisar recompilar para ajustar a lógica de calibração.
3. Um mecanismo de **calibração automática**: o relatório de análise diária (gerado por LLM, ver `prompt_analise_bra50.md`) passa a emitir, além do Markdown, um bloco JSON com os parâmetros calibrados do dia, que é salvo como arquivo de config e recarregado pelo Config Watcher — fechando o loop análise → parâmetro → execução, sem intervenção manual.

Este não é um recurso exclusivo do Bra50: a ideia é que qualquer indicador composto futuro (nova cesta de ações, outro tipo de score, outro terminal de dados) reaproveite o mesmo mecanismo.

## 1. Nova interface: `IIndicadorMultiFonte`

A interface atual `IIndicador` continua existindo para indicadores de série única (EMA, RSI, ATR, MACD), sem mudanças:

```csharp
public interface IIndicador
{
    string Nome { get; }
    ResultadoIndicador Calcular(IEnumerable<Quote> candles, IDictionary<string, object> parametros);
}
```

Adicionar uma nova interface para indicadores que precisam buscar dados de outros símbolos/terminais por conta própria (não recebem só a série do símbolo local):

```csharp
public interface IIndicadorMultiFonte
{
    string Nome { get; }

    /// <summary>
    /// Calcula o indicador buscando dados de um ou mais terminais/símbolos definidos em parametros.
    /// Recebe o dicionário de gateways disponíveis (por terminalId) para poder consultar qualquer terminal conectado,
    /// não só o do símbolo que está sendo negociado.
    /// </summary>
    Task<ResultadoIndicador> CalcularAsync(
        IReadOnlyDictionary<string, IGatewayMt5> gatewaysPorTerminal,
        IDictionary<string, object> parametros,
        CancellationToken ct);
}
```

`ResultadoIndicador` não muda — continua `(DateTime Timestamp, double? Valor)`. Para indicadores compostos, `Valor` é o score final (ex.: -100 a +100), e detalhes por símbolo vão para o log, não para o resultado estruturado (manter o contrato simples).

## 2. Onde os gateways por terminal já existem / precisam ser expostos

O `ConnectionManager` (Fase 2) já mantém um `IGatewayMt5` por `terminalId`. Hoje o `StrategyEngine` recebe só o gateway do seu próprio terminal (`_gateway`, injetado por config). Para os indicadores multi-fonte funcionarem, o `StrategyEngineFactory` (ou o próprio `ConnectionManager`) precisa expor um dicionário `IReadOnlyDictionary<string, IGatewayMt5>` com todos os terminais conectados, e passar essa referência para o `CatalogoIndicadores` (ou para um novo `CatalogoIndicadoresMultiFonte` paralelo) no momento de calcular.

Se o `ConnectionManager` já guarda os gateways internamente por `terminalId`, adicionar um método `ObterTodosGatewaysAsync()` ou uma propriedade somente-leitura é suficiente — não deve exigir mudança estrutural grande.

## 3. Indicador composto de referência: `ForcaCestaIndicador`

Implementação de referência para o caso do Bra50 — mas escrita de forma genérica (não hardcoded para VALE3/PETR4/ITUB4/BBDC4), controlada 100% por `parametros`:

```csharp
public sealed class ForcaCestaIndicador : IIndicadorMultiFonte
{
    public string Nome => "ForcaCesta";

    public async Task<ResultadoIndicador> CalcularAsync(
        IReadOnlyDictionary<string, IGatewayMt5> gatewaysPorTerminal,
        IDictionary<string, object> parametros,
        CancellationToken ct)
    {
        // parametros esperados:
        // "terminalId": "genial"
        // "timeframe": "D1"
        // "componentes": [
        //   { "simbolo": "VALE3", "peso": 0.30 },
        //   { "simbolo": "PETR4", "peso": 0.25 },
        //   { "simbolo": "ITUB4", "peso": 0.25 },
        //   { "simbolo": "BBDC4", "peso": 0.20 }
        // ]
        // "modoCalculo": "variacaoPercentualDia" | "cruzamentoEma"
        //
        // Retorna um score ponderado entre -100 (todos os componentes caindo forte,
        // pesos aplicados) e +100 (todos subindo forte).

        var terminalId  = ObterString(parametros, "terminalId", "genial");
        var timeframe   = ObterString(parametros, "timeframe", "D1");
        var componentes = ObterComponentes(parametros); // (string Simbolo, double Peso)[]

        if (!gatewaysPorTerminal.TryGetValue(terminalId, out var gateway))
            return new ResultadoIndicador(DateTime.UtcNow, null); // terminal não conectado — log deve alertar no chamador

        double scoreAcumulado = 0;
        double pesoTotal = 0;

        foreach (var (simbolo, peso) in componentes)
        {
            var candles = await gateway.ObterCandlesAsync(simbolo, timeframe, 5, ct);
            var ultimos = candles.OrderBy(c => c.Tempo).ToList();
            if (ultimos.Count < 2) continue;

            var fechamentoAnterior = ultimos[^2].Fechamento;
            var fechamentoAtual    = ultimos[^1].Fechamento;
            if (fechamentoAnterior == 0) continue;

            var variacaoPercent = (fechamentoAtual - fechamentoAnterior) / fechamentoAnterior * 100.0;

            // Normaliza a variação percentual para uma escala de -100 a +100
            // (ex.: variação de ±3% já é considerada movimento forte para uma ação individual)
            var scoreComponente = Math.Clamp(variacaoPercent / 3.0 * 100.0, -100, 100);

            scoreAcumulado += scoreComponente * peso;
            pesoTotal += peso;
        }

        if (pesoTotal == 0) return new ResultadoIndicador(DateTime.UtcNow, null);

        var scoreFinal = scoreAcumulado / pesoTotal;
        return new ResultadoIndicador(DateTime.UtcNow, scoreFinal);
    }

    // ObterString / ObterComponentes: helpers de parsing tolerantes a JsonElement,
    // seguindo o mesmo padrão de ObterPeriodo já corrigido em EmaIndicador/RsiIndicador/AtrIndicador
    // (ver prompt_fix_bug_periodo_indicadores.md — reaproveitar essa lógica de conversão aqui também,
    // para não reintroduzir o mesmo tipo de bug).
}
```

Pontos importantes para quem implementar:
- **Reaproveitar o parser tolerante a `JsonElement`** já corrigido nos outros indicadores (bug histórico: `v is int` falha para valores vindos de JSON). Vale extrair isso para um helper compartilhado (`ParametroParser.ObterInt`, `ObterDouble`, `ObterString`, `ObterListaDeObjetos`) em vez de duplicar em cada indicador novo.
- O fator de normalização (`/ 3.0 * 100.0` no exemplo) deveria ele mesmo vir de `parametros` (ex.: `"amplitudeNormalizacaoPercent": 3.0`), não fixo no código — mantém tudo calibrável sem recompilar.
- Se um componente falhar ao buscar candle (símbolo não subscrito, terminal fora do ar), pular esse componente e seguir com os demais, mas **logar como Warning** — não travar o indicador inteiro por causa de uma ação problemática.

## 4. Consumo no `StrategyEngine` / `AvaliarDecisao`

O indicador composto entra como mais um filtro de confirmação, no mesmo espírito do filtro de RSI já existente sobre o cruzamento de EMA:

```csharp
// Prioridade 2.5 (entre EMA/RSI e modo observação): filtro de força de cesta, se configurado
var forcaCesta = indicadoresMultiFonte.FirstOrDefault(r => r.Config.Nome.Equals("ForcaCesta", StringComparison.OrdinalIgnoreCase));
if (forcaCesta.Resultado?.Valor is double score)
{
    var limiarForte = ObterDouble(forcaCesta.Config.Parametros, "limiarConfirmacaoCompra", 20);
    var limiarFraco = ObterDouble(forcaCesta.Config.Parametros, "limiarConfirmacaoVenda", -20);

    // Se configurado, o score da cesta pode reforçar OU bloquear o sinal de EMA já calculado.
    // Ex.: só confirma compra se score >= limiarForte; só confirma venda se score <= limiarFraco.
}
```

A forma exata de combinar (bloquear vs. só logar como contexto) fica a critério de quem implementar, mas deve ser **configurável** — ex.: um campo `"forcaCestaBloqueiaEntrada": true/false` no `SymbolConfig`, para permitir usar o indicador só como informação no log no início (fase de validação), sem risco de travar o robô por causa de um indicador novo ainda não calibrado.

## 5. Schema de config — novo bloco `indicadoresMultiFonte`

Extensão do `SymbolConfig` (arquivo `Bra50Aug26.config.json`, terminal de execução):

```json
{
  "symbol": "Bra50Aug26",
  "terminalId": "corretora-b",
  "indicadores": [
    { "nome": "EMA", "timeframe": "M1", "parametros": { "periodo": 9 } },
    { "nome": "EMA", "timeframe": "M1", "parametros": { "periodo": 21 } }
  ],
  "indicadoresMultiFonte": [
    {
      "nome": "ForcaCesta",
      "parametros": {
        "terminalId": "genial",
        "timeframe": "D1",
        "amplitudeNormalizacaoPercent": 3.0,
        "componentes": [
          { "simbolo": "VALE3", "peso": 0.30 },
          { "simbolo": "PETR4", "peso": 0.25 },
          { "simbolo": "ITUB4", "peso": 0.25 },
          { "simbolo": "BBDC4", "peso": 0.20 }
        ],
        "limiarConfirmacaoCompra": 20,
        "limiarConfirmacaoVenda": -20,
        "forcaCestaBloqueiaEntrada": false
      }
    }
  ]
}
```

Todo valor numérico (pesos, limiares, amplitude de normalização) é o que vai ser recalibrado automaticamente pelo relatório diário — ver seção 6.

## 6. Loop de calibração automática (análise → config → execução)

O `prompt_analise_bra50.md` (já criado) passa a ter uma seção adicional obrigatória no final do relatório: além do Markdown de leitura humana, a IA deve emitir um bloco JSON isolado, delimitado claramente, contendo só os parâmetros calibráveis:

````markdown
## Parâmetros calibrados (uso automático — não editar manualmente)

```json
{
  "indicadoresMultiFonte": [
    {
      "nome": "ForcaCesta",
      "parametros": {
        "componentes": [
          { "simbolo": "VALE3", "peso": 0.32 },
          { "simbolo": "PETR4", "peso": 0.22 },
          { "simbolo": "ITUB4", "peso": 0.26 },
          { "simbolo": "BBDC4", "peso": 0.20 }
        ],
        "limiarConfirmacaoCompra": 18,
        "limiarConfirmacaoVenda": -22
      }
    }
  ]
}
```
````

Os pesos devem refletir a leitura do dia — ex.: se o relatório concluiu que Vale está sendo o driver dominante do pregão (minério em alta forte), o peso de VALE3 deveria subir na calibração daquele dia, dentro de limites razoáveis (a IA não deve zerar os outros pesos nem sair de uma faixa 0–0.5 por componente, para evitar um relatório ruim quebrar a diversificação do score de uma vez).

Um script/processo (pode ser um passo manual no início, ou automatizado depois) extrai esse bloco JSON do relatório e faz merge no `Bra50Aug26.config.json`, preservando os campos que não vêm do relatório (símbolo, terminalId, indicadores clássicos EMA, gestão de risco). O Config Watcher já existente detecta a mudança do arquivo e recarrega — **nenhuma mudança é necessária no mecanismo de hot-reload**, só no processo que gera/funde o novo bloco de parâmetros.

Recomendação: **não deixar a IA sobrescrever o arquivo de config inteiro diretamente** — ela deve gerar um arquivo separado (ex.: `Bra50Aug26.parametros-calibrados.json`) e um passo de merge (script simples ou lógica no próprio Config Watcher) aplica só o bloco `indicadoresMultiFonte.*.parametros` por cima do config existente. Isso evita que um erro de formatação no relatório apague campos críticos de risco/execução.

## 7. Ordem de implementação sugerida

1. `IIndicadorMultiFonte` + helper de parsing tolerante compartilhado (`ParametroParser`).
2. Exposição do dicionário de gateways por terminal (`ConnectionManager` → `StrategyEngineFactory`).
3. `ForcaCestaIndicador` implementado e testado isoladamente (unit test com candles mockados de 2+ símbolos, verificando o score ponderado).
4. Integração no `StrategyEngine.AvaliarEntradaAsync`/`AvaliarDecisao`, com `forcaCestaBloqueiaEntrada: false` como padrão inicial (só log, sem travar nada) até validar em campo.
5. `Bra50Aug26.config.json` criado com o bloco `indicadoresMultiFonte` (pesos iniciais podem ser os da carteira teórica do Ibovespa: VALE3 0.30, PETR4 0.25, ITUB4 0.25, BBDC4 0.20 — ver `ideias_bra50_correlacao.md`).
6. Processo de merge do bloco de parâmetros calibrados do relatório diário no config (pode começar manual/copiar-colar, automatizar depois que o formato estiver estável).

## Critério de aceite

- [ ] `IIndicadorMultiFonte` implementado e registrado em um catálogo próprio, sem quebrar o catálogo de indicadores de série única existente.
- [ ] `ForcaCestaIndicador` calcula corretamente um score ponderado a partir de candles reais de múltiplos símbolos do terminal Genial (validável comparando manualmente 2-3 casos: todas as ações subindo → score positivo alto; ações mistas → score próximo de zero).
- [ ] Todo parâmetro do indicador (pesos, limiares, amplitude de normalização, símbolos incluídos) é lido de `parametros` no config — nenhum valor hardcoded no C#.
- [ ] `StrategyEngine` consegue usar o resultado do `ForcaCesta` como log/contexto sem quebrar o fluxo de decisão existente (EMA/RSI continuam funcionando exatamente como antes quando `indicadoresMultiFonte` não está presente no config).
- [ ] Fluxo de merge do bloco JSON do relatório diário no config documentado e testável manualmente pelo menos uma vez, mesmo que o processo completo de automação venha depois.
