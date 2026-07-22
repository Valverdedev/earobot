using System.Text.Json;
using System.Text.Json.Serialization;

namespace Financial.Robot.Worker.Strategy.Interpretada;

public sealed record DefinicaoEstrategia
{
    [JsonPropertyName("$schemaVersion")]
    public string? SchemaVersion { get; init; }

    public string? Nome { get; init; }
    public string? Descricao { get; init; }
    public string? GeradoEm { get; init; }
    public string? GeradoPor { get; init; }

    public IReadOnlyList<FiltroDef>? Filtros { get; init; }

    public BlocoLado? Compra { get; init; }
    public BlocoLado? Venda { get; init; }
    public SaidaDefinicao? Saida { get; init; }
}

public sealed record SaidaDefinicao
{
    public CondicaoDef? Compra { get; init; }
    public CondicaoDef? Venda { get; init; }
}

public sealed record FiltroDef
{
    public string? Tipo { get; init; }
    public double? ValorPreco { get; init; }
    public int? Periodo { get; init; }
    public string? Inicio { get; init; }
    public string? Fim { get; init; }
    public double? Multiplo { get; init; }
    public int? JanelaCandles { get; init; }
    public double? MultiploRange { get; init; }
    public int? LookbackMedioRange { get; init; }
}

public sealed record BlocoLado
{
    public CondicaoDef? Setup { get; init; }
    public CondicaoDef? Gatilho { get; init; }
    public StopDef? Stop { get; init; }
}

public sealed record CondicaoDef
{
    // Agrupamento lógico
    public IReadOnlyList<CondicaoDef>? Todas { get; init; }
    public IReadOnlyList<CondicaoDef>? Qualquer { get; init; }

    // Operadores
    public string? Op { get; init; }
    public string? A { get; init; }
    
    [JsonConverter(typeof(OperandoFlexConverter))]
    public string? B { get; init; }
    
    [JsonConverter(typeof(OperandoFlexConverter))]
    public string? Min { get; init; }
    
    [JsonConverter(typeof(OperandoFlexConverter))]
    public string? Max { get; init; }
    
    public double? ToleranciaPreco { get; init; }
    public double? ValorPreco { get; init; }
    public IReadOnlyList<string>? Serie { get; init; }
    public string? Ordem { get; init; }
    public int? Candles { get; init; }
    public string? Direcao { get; init; }

    // Padrões
    public string? Padrao { get; init; }
    public int? Candle { get; init; }
    public double? MultiploPavio { get; init; }
    public double? CorpoMinimoFracaoRange { get; init; }
    public string? TipoNivel { get; init; }
    public string? Lado { get; init; }
    
    // Propriedades para Padrões Compostos
    public int? MinCandlesImpulso { get; init; }
    public int? MaxCandlesImpulso { get; init; }
    public int? MinCandlesPullback { get; init; }
    public int? MaxCandlesPullback { get; init; }
    public double? MaxPullbackPercentual { get; init; }
    public int? InicioCandleSinal { get; init; }
    public int? FimCandleSinal { get; init; }
    public string? IdRef { get; init; }
    public double? ClimaxMultiploRange { get; init; }
    public int? LookbackMedioRange { get; init; }
}

public sealed record StopDef
{
    public string? Tipo { get; init; }
    public int? Candle { get; init; }
    public string? Lado { get; init; }
    public double? BufferPreco { get; init; }
    public string? TipoNivel { get; init; }
    public string? Ref { get; init; }
    
    [JsonConverter(typeof(OperandoFlexConverter))]
    public string? A { get; init; }
}

public sealed class OperandoFlexConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}
