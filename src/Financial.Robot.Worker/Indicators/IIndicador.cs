using Financial.Robot.Domain.ValueObjects;
using Skender.Stock.Indicators;

namespace Financial.Robot.Worker.Indicators;

/// <summary>
/// Contrato para um indicador técnico calculado a partir de candles.
/// </summary>
public interface IIndicador
{
    /// <summary>Nome único do indicador, usado como chave no catálogo.</summary>
    string Nome { get; }

    /// <summary>Calcula o indicador e retorna o resultado mais recente.</summary>
    ResultadoIndicador Calcular(IEnumerable<Quote> candles, IDictionary<string, object> parametros);
}
