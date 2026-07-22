# Prompt para corrigir bug — parâmetro "periodo" dos indicadores é ignorado

## Contexto

Validando o Strategy Engine em produção (fim de semana, ativos cripto), encontrei um bug confirmado por dois caminhos: (1) recalculando EMA9/EMA21/RSI14/ATR14 manualmente a partir de candles reais do MT5 e comparando com o que o serviço logou, e (2) lendo o código-fonte direto.

## Bug

Em `EmaIndicador.cs`, `RsiIndicador.cs` e `AtrIndicador.cs` (pasta `Financial.Robot.Worker/Indicators/`), o método `ObterPeriodo` está assim nos três arquivos:

```csharp
private static int ObterPeriodo(IDictionary<string, object> parametros, int padrao) =>
    parametros.TryGetValue("periodo", out var v) && v is int i ? i : padrao;
```

O `v is int i` falha silenciosamente porque o valor numérico vindo da desserialização do JSON de config não chega como `int` nativo do C# (chega como `JsonElement`, `long` ou `double`, dependendo de como o `Dictionary<string, object>` foi desserializado). Resultado: a condição `v is int` é sempre falsa, e o método **sempre retorna o valor padrão**, ignorando o `"periodo"` configurado no JSON.

Isso é mascarado no RSI e no ATR porque o padrão (14) coincide com o valor configurado nos configs atuais (`"periodo": 14`). Mas no EMA fica visível: os dois indicadores EMA do config (período 9 e período 21) caem os dois no padrão (20), produzindo o mesmo resultado — por isso o log mostra `EMA(M1)` com o mesmo valor duas vezes seguidas, e o cruzamento EMA9×EMA21 que decide entrada nunca funciona corretamente.

## Correção

Trocar `ObterPeriodo` nos três arquivos (`EmaIndicador.cs`, `RsiIndicador.cs`, `AtrIndicador.cs`) para converter o valor de forma tolerante ao tipo real vindo do desserializador, em vez de exigir `int` exato. Sugestão:

```csharp
private static int ObterPeriodo(IDictionary<string, object> parametros, int padrao)
{
    if (!parametros.TryGetValue("periodo", out var v) || v is null)
        return padrao;

    return v switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        System.Text.Json.JsonElement je when je.TryGetInt32(out var jv) => jv,
        _ => Convert.ToInt32(v)
    };
}
```

(Ajustar conforme o tipo real que aparecer em runtime — se o projeto usa `System.Text.Json` para desserializar o `Dictionary<string, object>`, o caso mais provável é `JsonElement`. Se usa `Newtonsoft.Json`, o mais provável é `long`. Adicionar um teste unitário cobrindo o tipo real evita reintroduzir esse bug.)

## Onde aplicar

- `Financial.Robot.Worker/Indicators/EmaIndicador.cs`
- `Financial.Robot.Worker/Indicators/RsiIndicador.cs`
- `Financial.Robot.Worker/Indicators/AtrIndicador.cs`
- Verificar se `MacdIndicador.cs` (citado no `CatalogoIndicadores`, mas não inspecionado ainda) tem o mesmo padrão de `ObterPeriodo` — se sim, aplicar a mesma correção lá.

## Como validar que corrigiu

1. Rodar o serviço com `BTCLTC.config.json` (ou outro ativo cripto) carregado e observar o log no próximo candle fechado: as duas linhas de `EMA(M1)` devem mostrar **valores diferentes** entre si (uma pro período 9, outra pro período 21).
2. Comparar com cálculo independente: puxar os mesmos candles via `get_candles_latest` no MetaTrader e recalcular EMA9/EMA21/RSI14/ATR14 manualmente (Python/pandas ou equivalente) — os valores logados devem bater com a conta manual dentro de uma margem pequena de arredondamento.
3. Confirmar que, com os períodos corretos, o cruzamento EMA9×EMA21 eventualmente dispara uma decisão de compra/venda (`AvaliarDecisao` retornando `Comprar`/`Vender`) quando as condições reais do mercado o justificarem — hoje isso está efetivamente travado porque EMA9 e EMA21 nunca cruzam de verdade (calculam o mesmo valor).
4. Rodar os testes unitários existentes (`Financial.Robot.Domain.Tests`, `Financial.Robot.Application.Tests`) e adicionar um teste novo para `ObterPeriodo` cobrindo explicitamente o tipo que vem do desserializador real do projeto, para não regressar.
