using Financial.Robot.Domain.Interfaces;

namespace Financial.Robot.Worker.Indicators;

/// <summary>
/// Contrato para um indicador técnico que busca dados de múltiplos terminais/símbolos.
/// </summary>
public interface IIndicadorMultiFonte
{
    string Nome { get; }

    /// <summary>
    /// Calcula o indicador buscando dados de um ou mais terminais/símbolos definidos em parametros.
    /// Recebe o dicionário de gateways disponíveis (por terminalId) para poder consultar qualquer terminal conectado.
    /// </summary>
    Task<ResultadoIndicador> CalcularAsync(
        IReadOnlyDictionary<string, IGatewayMt5> gatewaysPorTerminal,
        IDictionary<string, object>? parametros,
        CancellationToken ct);
}
