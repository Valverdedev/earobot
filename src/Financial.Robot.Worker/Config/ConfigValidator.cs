using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Config;

public class ConfigValidator
{
    private readonly ILogger<ConfigValidator> _logger;
    private readonly IConnectionManager _connectionManager;

    public ConfigValidator(ILogger<ConfigValidator> logger, IConnectionManager connectionManager)
    {
        _logger = logger;
        _connectionManager = connectionManager;
    }

    public async Task<bool> IsValidAsync(SymbolConfig config)
    {
        if (config == null) return false;

        if (string.IsNullOrWhiteSpace(config.Symbol))
        {
            _logger.LogError("Configuração inválida: 'symbol' é obrigatório.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.TerminalId))
        {
            _logger.LogError("Configuração {Symbol} inválida: 'terminalId' é obrigatório.", config.Symbol);
            return false;
        }

        var connectedTerminals = _connectionManager.GetConnectedTerminalIds();
        if (!connectedTerminals.Contains(config.TerminalId))
        {
            _logger.LogError("Configuração {Symbol} rejeitada: 'terminalId' {TerminalId} não está configurado/conectado.", config.Symbol, config.TerminalId);
            return false;
        }

        if (config.Saida != null)
        {
            var errosSaida = Execution.ValidadorSaidaConfig.Validar(config.Saida);
            if (errosSaida.Count > 0)
            {
                foreach (var erro in errosSaida)
                {
                    _logger.LogError("Configuração {Symbol} rejeitada (Saída): {Erro}", config.Symbol, erro);
                }
                return false;
            }
        }

        // Validação de SL vs Spread (se validável estaticamente)
        if (config.Saida?.SlMinimoSobreSpread.HasValue == true && config.Saida?.StopLossPips.HasValue == true)
        {
            if (config.Saida.UnidadeDistancia == UnidadeDistancia.Pips || config.Saida.UnidadeDistancia == UnidadeDistancia.PrecoAbsoluto)
            {
                try
                {
                    var client = _connectionManager.GetClient(config.TerminalId);
                    var tick = await client.ObterTickAtualAsync(config.BrokerSymbol ?? config.Symbol);
                    var spread = (decimal)(tick.Ask - tick.Bid);
                    
                    if (spread > 0)
                    {
                        decimal slAbsoluto = 0;
                        if (config.Saida.UnidadeDistancia == UnidadeDistancia.PrecoAbsoluto)
                        {
                            slAbsoluto = config.Saida.StopLossPips.Value;
                        }
                        else if (config.Saida.UnidadeDistancia == UnidadeDistancia.Pips)
                        {
                            var pointSize = (decimal)await client.ObterTamanhoPontoAsync(config.BrokerSymbol ?? config.Symbol);
                            // Assumindo que StopLossPips na verdade se refere a pips, e 1 pip = 10 points, 
                            // mas em MT5 Point é a menor unidade. Vou considerar 1 Pip = 10 * Point para Forex (padrão comum),
                            // ou usar StopLossPips diretamente como multiplicador do Point. O ideal é 10 * Point.
                            slAbsoluto = config.Saida.StopLossPips.Value * (pointSize * 10);
                        }

                        var slMinimoEsperado = spread * config.Saida.SlMinimoSobreSpread.Value;
                        if (slAbsoluto < slMinimoEsperado)
                        {
                            _logger.LogError(
                                "Configuração {Symbol} rejeitada: StopLoss muito curto ({SlAbsoluto:F5}) comparado ao limite mínimo sobre o spread ({SlMinimoEsperado:F5}) [Spread={Spread:F5}, Fator={Fator}x].", 
                                config.Symbol, slAbsoluto, slMinimoEsperado, spread, config.Saida.SlMinimoSobreSpread.Value);
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Não foi possível validar o SlMinimoSobreSpread para {Symbol}. Terminal={Terminal}", config.Symbol, config.TerminalId);
                    // Como não conseguiu consultar, não falha a carga para não quebrar em caso de instabilidade, mas alerta.
                }
            }
        }

        // Avisos de coerência (não invalidam a config, mas alertam)
        if (config.Comprar && config.Entrada != null)
        {
            if (!config.Entrada.ComprarAcimaDe.HasValue && !config.Entrada.ComprarAbaixoDe.HasValue)
            {
                _logger.LogWarning("Configuração {Symbol}: 'comprar' é true mas nenhum nível de entrada de compra foi definido.", config.Symbol);
            }
        }

        if (config.Vender && config.Entrada != null)
        {
            if (!config.Entrada.VenderAcimaDe.HasValue && !config.Entrada.VenderAbaixoDe.HasValue)
            {
                _logger.LogWarning("Configuração {Symbol}: 'vender' é true mas nenhum nível de entrada de venda foi definido.", config.Symbol);
            }
        }

        return true;
    }
}
