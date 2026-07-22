# Spec Fase 7 — Corrigir implementação real das estratégias Price Action, ORB e Reversão de Range

## Motivação

Revisão de código pós-Fase 5 encontrou que só a `CruzamentoEma` implementa a lógica realmente especificada (inclusive já integra o filtro `ForcaCesta` corretamente). As outras três estratégias existem como classes registradas e funcionais (não quebram o build, não travam o robô), mas o **conteúdo da decisão não corresponde ao que foi desenhado**:

- `PriceActionSuporteResistencia`: espera `niveis` como `List<double>` simples. O bloco de calibração já emitido pelos prompts de análise (`prompt_analise_bra50.md`, `prompt_analise_hk50.md`, `prompt_analise_gold.md`) e a spec original (`spec_fase5_estrategias_plugaveis.md`) usam objetos `{"tipo": "suporte"|"resistencia", "preco": N}`. Isso não desserializa como `List<double>` — cai no `catch` e a estratégia fica permanentemente em "Aguardar" sem nenhum log de erro visível além de um `Motivo` genérico. Além disso, a lógica de bounce atual não diferencia suporte de resistência e não implementa nem rompimento-com-reteste nem rejeição como desenhado.
- `OpeningRangeBreakout`: não forma o range a partir dos candles da janela de abertura — só reaproveita `Entrada.ComprarAcimaDe`/`VenderAbaixoDe` fixos, que é a mesma lógica de nível fixo de preço que já existia antes da Fase 5.
- `ReversaoRange`: mesmo problema — só reaproveita `Entrada.ComprarAbaixoDe`/`VenderAcimaDe` fixos, sem usar RSI em extremo nem os níveis parametrizados via `parametros`.

Nenhuma dessas três está pronta para operar de verdade com a lógica pretendida. Como o usuário confirmou que quer rodar essas estratégias com ordem real (mesmo que em conta demo), corrigir isso é bloqueante antes de ativá-las.

## Convenções já estabelecidas no código (seguir exatamente)

- Parâmetros de estratégia vêm em `EstrategiaConfig.Parametros` (`Dictionary<string, object>?`), populado a partir do JSON de config — valores numéricos chegam como `JsonElement`, nunca como `int`/`double` nativos diretamente.
- Já existe `Financial.Robot.Worker.Indicators.ParametroParser` (estático) com `ObterInt`, `ObterDouble`, `ObterString`, `ObterComponentes`, `ObterJanelaHorario` — todos tolerantes a `JsonElement`. Reaproveitar esse helper em vez de reimplementar parsing ad-hoc (como as classes atuais fazem com métodos privados `ObterDouble` duplicados).
- `CandleMt5` tem `Tempo, Abertura, Maximo, Minimo, Fechamento, Volume` — sem propriedade calculada de "pavio" ou "corpo", precisa calcular inline quando necessário.
- `IEstrategiaEntrada.Avaliar` é **stateless por chamada** — recebe `IReadOnlyList<CandleMt5> candles` (histórico completo carregado pela engine, não só o candle atual). Toda lógica que precise de "memória" (ex.: range formado às 9h, padrão de 2-3 candles) deve ser recalculada a partir desse histórico a cada chamada, não guardada em campo de instância (a mesma instância de estratégia é reaproveitada entre símbolos/engines diferentes via `CatalogoEstrategias`, então não deve ter estado mutável específico de uma engine).
- `JanelaHorarioPermitido` de cada estratégia já é validado pelo `RiskGuard.ValidarJanelaHorario` no `StrategyEngine`, **depois** de `estrategia.Avaliar` retornar uma decisão — ou seja, as estratégias não precisam reimplementar a checagem de horário permitido, só precisam saber o horário atual quando a própria lógica depende disso (caso do ORB, que precisa saber se está na janela de formação ou na janela de operação).

## 1. Novo helper: `ParametroParser.ObterNiveis`

Adicionar ao `ParametroParser.cs`, seguindo o padrão de `ObterComponentes`:

```csharp
public static IReadOnlyList<(string Tipo, double Preco)> ObterNiveis(IDictionary<string, object>? parametros, string chave = "niveis")
{
    var result = new List<(string, double)>();
    if (parametros == null || !parametros.TryGetValue(chave, out var v) || v is null)
        return result;

    if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in je.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            string? tipo = null;
            double preco = 0;

            if (item.TryGetProperty("tipo", out var propTipo))
                tipo = propTipo.GetString();

            if (item.TryGetProperty("preco", out var propPreco) && propPreco.ValueKind == JsonValueKind.Number)
                propPreco.TryGetDouble(out preco);

            if (!string.IsNullOrEmpty(tipo))
                result.Add((tipo, preco));
        }
    }

    return result;
}
```

Isso passa a bater exatamente com o schema `{"tipo": "suporte", "preco": 24000}` já usado nos três prompts de análise em produção — nenhum prompt precisa mudar.

## 2. `PriceActionSuporteResistencia` — reescrever

Implementar as duas variantes já desenhadas na spec original (rompimento com reteste, e rejeição no nível), selecionáveis via parâmetro:

```csharp
public sealed class PriceActionSuporteResistencia : IEstrategiaEntrada
{
    public string Nome => "PriceActionSuporteResistencia";

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var niveis = ParametroParser.ObterNiveis(config.Parametros);
        if (niveis.Count == 0)
            return ResultadoDecisao.Aguardar("Nenhum nível de suporte/resistência configurado.");

        var tolerancia = ParametroParser.ObterDouble(config.Parametros, "toleranciaRompimentoPontos", 50);
        var exigeReteste = ParametroParser.ObterString(config.Parametros, "exigeRetesteConfirmado", "true") == "true";
        var candlesConfirmacao = ParametroParser.ObterInt(config.Parametros, "candlesConfirmacao", 2);

        if (candles.Count < candlesConfirmacao + 3)
            return ResultadoDecisao.Aguardar("Histórico insuficiente para avaliar Price Action.");

        var recentes = candles.OrderBy(c => c.Tempo).ToList();

        foreach (var (tipo, preco) in niveis)
        {
            var decisao = exigeReteste
                ? AvaliarRompimentoComReteste(recentes, tipo, preco, tolerancia, candlesConfirmacao, config)
                : AvaliarRejeicao(recentes, tipo, preco, tolerancia, config);

            if (decisao is not null) return decisao;
        }

        return ResultadoDecisao.Aguardar("Nenhum gatilho de Price Action nos níveis configurados.");
    }

    // Rompimento: candle fecha além do nível + tolerância; reteste: candle seguinte volta e testa sem fechar do lado errado;
    // confirmação: candle fecha na direção do rompimento original.
    private ResultadoDecisao? AvaliarRompimentoComReteste(
        List<CandleMt5> candles, string tipo, double nivel, double tolerancia, int candlesConfirmacao, EstrategiaConfig config)
    {
        // Procura, nos últimos (candlesConfirmacao + 2) candles, o padrão: rompimento -> reteste -> confirmação
        var janela = candles.TakeLast(candlesConfirmacao + 2).ToList();
        if (janela.Count < 3) return null;

        var rompimento = janela[0];
        var romperCima = rompimento.Fechamento > nivel + tolerancia;
        var romperBaixo = rompimento.Fechamento < nivel - tolerancia;
        if (!romperCima && !romperBaixo) return null;

        var reteste = janela.Skip(1).Take(janela.Count - 2);
        var retesteValido = romperCima
            ? reteste.All(c => c.Minimo >= nivel - tolerancia)   // não voltou a fechar abaixo do nível
            : reteste.All(c => c.Maximo <= nivel + tolerancia);  // não voltou a fechar acima do nível
        if (!retesteValido) return null;

        var confirmacao = janela[^1];
        if (romperCima && confirmacao.Fechamento > rompimento.Maximo && config.Comprar)
            return ResultadoDecisao.Comprar($"PriceAction: rompimento+reteste confirmado acima de {nivel} ({tipo})");

        if (romperBaixo && confirmacao.Fechamento < rompimento.Minimo && config.Vender)
            return ResultadoDecisao.Vender($"PriceAction: rompimento+reteste confirmado abaixo de {nivel} ({tipo})");

        return null;
    }

    // Rejeição: candle mais recente tem pavio longo do lado do nível e fecha do lado oposto.
    private ResultadoDecisao? AvaliarRejeicao(List<CandleMt5> candles, string tipo, double nivel, double tolerancia, EstrategiaConfig config)
    {
        var ultimo = candles[^1];
        var perto = Math.Abs(ultimo.Minimo - nivel) <= tolerancia || Math.Abs(ultimo.Maximo - nivel) <= tolerancia;
        if (!perto) return null;

        var corpo = Math.Abs(ultimo.Fechamento - ultimo.Abertura);
        var pavioInferior = Math.Min(ultimo.Abertura, ultimo.Fechamento) - ultimo.Minimo;
        var pavioSuperior = ultimo.Maximo - Math.Max(ultimo.Abertura, ultimo.Fechamento);

        // Rejeição de baixa (suporte segurou): pavio inferior longo, fecha acima da abertura
        if (pavioInferior > corpo * 1.5 && ultimo.Fechamento > ultimo.Abertura && config.Comprar)
            return ResultadoDecisao.Comprar($"PriceAction: rejeição de baixa no nível {nivel} ({tipo})");

        // Rejeição de alta (resistência segurou): pavio superior longo, fecha abaixo da abertura
        if (pavioSuperior > corpo * 1.5 && ultimo.Fechamento < ultimo.Abertura && config.Vender)
            return ResultadoDecisao.Vender($"PriceAction: rejeição de alta no nível {nivel} ({tipo})");

        return null;
    }
}
```

Nota: o campo `tipo` (`"suporte"`/`"resistencia"`) fica disponível para uso futuro (ex.: só permitir compra em suporte e venda em resistência, hoje a lógica de rompimento permite qualquer direção em qualquer nível, o que é razoável para rompimento mas pode ser refinado depois). Documentar essa simplificação no código como comentário.

## 3. `OpeningRangeBreakout` — reescrever

```csharp
public sealed class OpeningRangeBreakout : IEstrategiaEntrada
{
    public string Nome => "OpeningRangeBreakout";

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var janelaFormacao = ParametroParser.ObterJanelaHorario(config.Parametros, "janelaFormacaoRange");
        var janelaOperacao = ParametroParser.ObterJanelaHorario(config.Parametros, "janelaOperacao");
        if (janelaFormacao is null || janelaOperacao is null)
            return ResultadoDecisao.Aguardar("janelaFormacaoRange/janelaOperacao não configuradas para ORB.");

        var agora = DateTime.UtcNow;
        var horaAtual = agora.TimeOfDay;

        var dentroOperacao = DentroDaJanela(horaAtual, janelaOperacao.Value);
        if (!dentroOperacao)
            return ResultadoDecisao.Aguardar("Fora da janela de operação do ORB.");

        // Recalcula o range a partir dos candles de hoje dentro da janela de formação
        var candlesHoje = candles.Where(c => c.Tempo.Date == agora.Date).ToList();
        var candlesRange = candlesHoje.Where(c => DentroDaJanela(c.Tempo.TimeOfDay, janelaFormacao.Value)).ToList();

        if (candlesRange.Count == 0)
            return ResultadoDecisao.Aguardar("Range de abertura ainda não formado (sem candles na janela de formação hoje).");

        var maxRange = candlesRange.Max(c => c.Maximo);
        var minRange = candlesRange.Min(c => c.Minimo);

        if (config.Comprar && tick.Ask > maxRange)
            return ResultadoDecisao.Comprar($"ORB: rompimento acima da máxima do range ({maxRange:F2})");

        if (config.Vender && tick.Bid < minRange)
            return ResultadoDecisao.Vender($"ORB: rompimento abaixo da mínima do range ({minRange:F2})");

        return ResultadoDecisao.Aguardar($"ORB: dentro do range ({minRange:F2}-{maxRange:F2}).");
    }

    private static bool DentroDaJanela(TimeSpan agora, (TimeSpan Inicio, TimeSpan Fim) janela) =>
        janela.Inicio <= janela.Fim
            ? agora >= janela.Inicio && agora <= janela.Fim
            : agora >= janela.Inicio || agora <= janela.Fim;
}
```

Atenção: `ParametroParser.ObterJanelaHorario` hoje espera uma **string única** no formato `"HH:mm-HH:mm"` (ver implementação atual, usa `Split('-')`), não o objeto `{"inicio": "09:00", "fim": "09:30"}` que a spec original desenhou. **Decisão necessária**: ou (a) ajustar o config do ORB para usar strings (`"janelaFormacaoRange": "09:00-09:30"`), reaproveitando o parser como está, ou (b) estender `ObterJanelaHorario` para aceitar ambos os formatos. Recomendo (a) por consistência — é mais simples e evita ambiguidade, só atualizar o exemplo de config na spec da Fase 5 e no README do ORB quando for escrito.

## 4. `ReversaoRange` — reescrever

```csharp
public sealed class ReversaoRange : IEstrategiaEntrada
{
    public string Nome => "ReversaoRange";

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var niveis = ParametroParser.ObterNiveis(config.Parametros);
        if (niveis.Count == 0)
            return ResultadoDecisao.Aguardar("Nenhum nível configurado para ReversaoRange.");

        var rsiVal = indicadores.FirstOrDefault(r => r.Config.Nome.Equals("RSI", StringComparison.OrdinalIgnoreCase)).Resultado?.Valor;
        if (!rsiVal.HasValue)
            return ResultadoDecisao.Aguardar("RSI não calculado — necessário para ReversaoRange.");

        var rsiSobrevenda = ParametroParser.ObterDouble(config.Parametros, "rsiSobrevendaMaximo", 30);
        var rsiSobrecompra = ParametroParser.ObterDouble(config.Parametros, "rsiSobrecompraMinimo", 70);
        var distanciaMaxima = ParametroParser.ObterDouble(config.Parametros, "distanciaMaximaDoNivelPontos", 100);

        var suporte = niveis.Where(n => n.Tipo.Equals("suporte", StringComparison.OrdinalIgnoreCase))
                             .OrderBy(n => Math.Abs(tick.Bid - n.Preco)).FirstOrDefault();
        var resistencia = niveis.Where(n => n.Tipo.Equals("resistencia", StringComparison.OrdinalIgnoreCase))
                                 .OrderBy(n => Math.Abs(tick.Bid - n.Preco)).FirstOrDefault();

        if (config.Comprar && suporte != default &&
            Math.Abs(tick.Bid - suporte.Preco) <= distanciaMaxima &&
            rsiVal <= rsiSobrevenda)
        {
            return ResultadoDecisao.Comprar($"ReversaoRange: RSI={rsiVal:F1} (sobrevenda) próximo ao suporte {suporte.Preco}");
        }

        if (config.Vender && resistencia != default &&
            Math.Abs(tick.Bid - resistencia.Preco) <= distanciaMaxima &&
            rsiVal >= rsiSobrecompra)
        {
            return ResultadoDecisao.Vender($"ReversaoRange: RSI={rsiVal:F1} (sobrecompra) próximo à resistência {resistencia.Preco}");
        }

        return ResultadoDecisao.Aguardar($"ReversaoRange: RSI={rsiVal:F1}, fora de condição de reversão.");
    }
}
```

Requer que a estratégia tenha um indicador `RSI` configurado em `EstrategiaConfig.Indicadores` (já é o caso no exemplo de config da spec Fase 5 — `ReversaoRange` tinha `RSI` M5 configurado). Nenhuma mudança necessária no `StrategyEngine` para isso, já é calculado e passado via `indicadores`.

## Critério de aceite

- [ ] `ParametroParser.ObterNiveis` implementado e usado tanto por `PriceActionSuporteResistencia` quanto por `ReversaoRange`.
- [ ] `PriceActionSuporteResistencia` desserializa corretamente o bloco `niveis` no formato `{"tipo": ..., "preco": ...}` já emitido pelos prompts de análise de Bra50/HK50/Gold, sem precisar mudar esses prompts.
- [ ] `PriceActionSuporteResistencia` implementa as duas variantes (rompimento+reteste e rejeição), selecionável via `exigeRetesteConfirmado`.
- [ ] `OpeningRangeBreakout` calcula o range dinamicamente a partir dos candles do dia dentro de `janelaFormacaoRange`, e só opera dentro de `janelaOperacao` — validável simulando um dia de candles com range conhecido e verificando que só dispara após ultrapassar máxima/mínima real do range formado.
- [ ] `ReversaoRange` usa RSI real (via indicador já configurado) e distância aos níveis parametrizados, não mais os campos fixos de `Entrada`.
- [ ] As 4 estratégias (`CruzamentoEma` incluída) continuam operando em paralelo sem erro de build ou de execução — validar com `dotnet build` e rodando pelo menos um ciclo em symbol de teste (ex.: BTCUSD) antes de aplicar a qualquer ativo novo.
- [ ] Config de exemplo da spec Fase 5 (`spec_fase5_estrategias_plugaveis.md`) atualizado para refletir o formato de string único em `janelaFormacaoRange`/`janelaOperacao` (`"09:00-09:30"`), consistente com `ParametroParser.ObterJanelaHorario` já existente.
