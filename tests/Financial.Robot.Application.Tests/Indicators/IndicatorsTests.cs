using System.Text.Json;
using Financial.Robot.Worker.Indicators;
using FluentAssertions;
using Skender.Stock.Indicators;
using Xunit;

namespace Financial.Robot.Application.Tests.Indicators;

public sealed class IndicatorsTests
{
    [Fact]
    public void Calcular_ComPeriodoComoJsonElement_DeveRespeitarPeriodoConfigurado()
    {
        // Arrange
        var indicador = new EmaIndicador();
        
        // Simula o System.Text.Json convertendo um número no Dictionary<string, object>
        var json = "{\"periodo\": 5}";
        var doc = JsonDocument.Parse(json);
        var jsonElement = doc.RootElement.GetProperty("periodo");
        
        var parametros = new Dictionary<string, object>
        {
            ["periodo"] = jsonElement
        };

        // Cria uma série de 10 quotes idênticos
        var quotes = Enumerable.Range(1, 10).Select(i => new Quote
        {
            Date = DateTime.UtcNow.AddMinutes(i),
            Open = 100,
            High = 100,
            Low = 100,
            Close = 100 + i
        }).ToList();

        // Act
        // Se usar o período 5, o cálculo da EMA funcionará
        // Se falhasse e caísse no default (20), o GetEma(20) retornaria vazio/NaN pois há apenas 10 quotes
        var resultado = indicador.Calcular(quotes, parametros);

        // Assert
        resultado.Should().NotBeNull();
        resultado.Valor.Should().NotBeNull();
        resultado.Valor.HasValue.Should().BeTrue();
        double.IsNaN(resultado.Valor!.Value).Should().BeFalse();
    }
}
