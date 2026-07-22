using Financial.Robot.Application.Configuracoes;
using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Excecoes;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Financial.Robot.Worker.Risk;

/// <summary>
/// Soma o resultado flutuante (não realizado) de TODAS as posições abertas
/// em todos os símbolos/estratégias ativos por terminal. Ao cruzar a meta
/// de lucro ou o stop de perda configurados (percentual ou absoluto), fecha
/// todas as posições abertas naquele terminal. Suporta múltiplos terminais
/// via lista de configurações.
/// </summary>
public sealed class AlvoGlobalPosicoesAbertasService : BackgroundService
{
    private readonly IOptionsMonitor<List<ConfiguracaoAlvoGlobalPosicoesAbertas>> _opcoes;
    private readonly ConfigWatcherService _configWatcher;
    private readonly IConnectionManager _connectionManager;
    private readonly IServicoExecucao _execucao;
    private readonly ILogger<AlvoGlobalPosicoesAbertasService> _logger;

    public AlvoGlobalPosicoesAbertasService(
        IOptionsMonitor<List<ConfiguracaoAlvoGlobalPosicoesAbertas>> opcoes,
        ConfigWatcherService configWatcher,
        IConnectionManager connectionManager,
        IServicoExecucao execucao,
        ILogger<AlvoGlobalPosicoesAbertasService> logger)
    {
        _opcoes = opcoes;
        _configWatcher = configWatcher;
        _connectionManager = connectionManager;
        _execucao = execucao;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var lista = _opcoes.CurrentValue;
            var intervalo = ObterIntervaloMinimo(lista);

            try
            {
                foreach (var cfg in lista.Where(EstaAtiva))
                    await ProcessarAsync(cfg, stoppingToken);
            }
            catch (ConexaoMt5Exception)
            {
                _logger.LogDebug("[AlvoGlobal] Sem conexão com MT5. Aguardando reconexão...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AlvoGlobal] Erro no ciclo de verificação.");
            }

            await Task.Delay(intervalo, stoppingToken);
        }
    }

    private async Task ProcessarAsync(ConfiguracaoAlvoGlobalPosicoesAbertas cfg, CancellationToken ct)
    {
        IGatewayMt5 gateway;
        try
        {
            gateway = _connectionManager.GetClient(cfg.TerminalId);
        }
        catch
        {
            _logger.LogWarning("[AlvoGlobal] Terminal {TerminalId} não conectado, pulando ciclo.", cfg.TerminalId);
            return;
        }

        var conta = await gateway.ObterInfoContaAsync(ct);
        if (conta.Saldo <= 0) return;

        var posicoesPorEstrategia = await ListarEstrategiasComLucroAbertoAsync(gateway, cfg.TerminalId, ct);
        if (posicoesPorEstrategia.Count == 0) return;

        var pnlFlutuanteTotal = posicoesPorEstrategia.Sum(p => p.LucroAberto);
        var motivo = DeterminarMotivo(cfg, pnlFlutuanteTotal, conta.Saldo);

        if (motivo is null)
        {
            LogarEstadoAtual(cfg, pnlFlutuanteTotal, conta.Saldo);
            return;
        }

        _logger.LogWarning("[AlvoGlobal] {Motivo} — fechando todas as posições abertas no terminal {TerminalId}.", motivo, cfg.TerminalId);
        await FecharTodasAsync(gateway, cfg.TerminalId, posicoesPorEstrategia, ct);
    }

    private static string? DeterminarMotivo(
        ConfiguracaoAlvoGlobalPosicoesAbertas cfg,
        double pnlFlutuanteTotal,
        double saldo)
    {
        return cfg.UnidadeAlvo switch
        {
            UnidadeAlvoGlobal.Percentual => DeterminarMotivoPercentual(cfg, pnlFlutuanteTotal, saldo),
            UnidadeAlvoGlobal.Absoluto => DeterminarMotivoAbsoluto(cfg, pnlFlutuanteTotal),
            _ => null
        };
    }

    private static string? DeterminarMotivoPercentual(
        ConfiguracaoAlvoGlobalPosicoesAbertas cfg,
        double pnlFlutuanteTotal,
        double saldo)
    {
        var pnlPercent = pnlFlutuanteTotal / saldo * 100.0;

        if (cfg.MetaLucroGlobalPercent is > 0 && pnlPercent >= cfg.MetaLucroGlobalPercent.Value)
            return $"Meta de lucro global atingida: {pnlPercent:F2}% >= {cfg.MetaLucroGlobalPercent.Value:F2}%";

        if (cfg.StopPerdaGlobalPercent is > 0 && pnlPercent <= -cfg.StopPerdaGlobalPercent.Value)
            return $"Stop de perda global atingido: {pnlPercent:F2}% <= -{cfg.StopPerdaGlobalPercent.Value:F2}%";

        return null;
    }

    private static string? DeterminarMotivoAbsoluto(
        ConfiguracaoAlvoGlobalPosicoesAbertas cfg,
        double pnlFlutuanteTotal)
    {
        if (cfg.MetaLucroGlobalAbsoluto is > 0 && pnlFlutuanteTotal >= cfg.MetaLucroGlobalAbsoluto.Value)
            return $"Meta de lucro absoluto atingida: {pnlFlutuanteTotal:F2} >= {cfg.MetaLucroGlobalAbsoluto.Value:F2}";

        if (cfg.StopPerdaGlobalAbsoluto is > 0 && pnlFlutuanteTotal <= -cfg.StopPerdaGlobalAbsoluto.Value)
            return $"Stop de perda absoluto atingido: {pnlFlutuanteTotal:F2} <= -{cfg.StopPerdaGlobalAbsoluto.Value:F2}";

        return null;
    }

    private void LogarEstadoAtual(
        ConfiguracaoAlvoGlobalPosicoesAbertas cfg,
        double pnlFlutuanteTotal,
        double saldo)
    {
        if (cfg.UnidadeAlvo == UnidadeAlvoGlobal.Percentual)
        {
            var pnlPercent = pnlFlutuanteTotal / saldo * 100.0;
            _logger.LogDebug("[AlvoGlobal] [{TerminalId}] Flutuante: {Pnl:F2} ({Percent:F2}% do saldo {Saldo:F2}).",
                cfg.TerminalId, pnlFlutuanteTotal, pnlPercent, saldo);
        }
        else
        {
            _logger.LogDebug("[AlvoGlobal] [{TerminalId}] Flutuante: {Pnl:F2} (absoluto).",
                cfg.TerminalId, pnlFlutuanteTotal);
        }
    }

    private async Task<List<(string Symbol, long Magic, double LucroAberto)>> ListarEstrategiasComLucroAbertoAsync(
        IGatewayMt5 gateway, string terminalId, CancellationToken ct)
    {
        var resultado = new List<(string, long, double)>();

        foreach (var config in _configWatcher.GetActiveConfigs())
        {
            if (config.TerminalId != terminalId) continue;

            var estrategias = config.Estrategias is { Count: > 0 }
                ? config.Estrategias
                : new List<EstrategiaConfig> { CriarEstrategiaFallback(config) };

            foreach (var est in estrategias.Where(e => e.Ativa))
            {
                var brokerSymbol = config.BrokerSymbol ?? config.Symbol;
                var lucroAberto = await gateway.ObterLucroAbertoAsync(brokerSymbol, est.MagicNumber, ct);
                if (lucroAberto != 0)
                    resultado.Add((brokerSymbol, est.MagicNumber, lucroAberto));
            }
        }

        return resultado;
    }

    private async Task FecharTodasAsync(
        IGatewayMt5 gateway, string terminalId,
        List<(string Symbol, long Magic, double LucroAberto)> estrategiasComPosicao, CancellationToken ct)
    {
        foreach (var (symbol, magic, _) in estrategiasComPosicao)
        {
            var tickets = await gateway.ObterTicketsPosicoesAbertasAsync(symbol, magic, ct);
            foreach (var ticket in tickets)
            {
                var resultado = await _execucao.FecharPosicaoAsync(terminalId, ticket, ct);
                if (resultado.Sucesso)
                    _logger.LogInformation("[AlvoGlobal] Posição fechada: {Simbolo} Ticket={Ticket} Magic={Magic}", symbol, ticket, magic);
                else
                    _logger.LogWarning("[AlvoGlobal] Falha ao fechar {Simbolo} Ticket={Ticket}: {Erro}", symbol, ticket, resultado.Motivo);
            }
        }
    }

    private static bool EstaAtiva(ConfiguracaoAlvoGlobalPosicoesAbertas cfg)
    {
        if (!cfg.Ativo || string.IsNullOrWhiteSpace(cfg.TerminalId)) return false;

        return cfg.UnidadeAlvo switch
        {
            UnidadeAlvoGlobal.Percentual => cfg.MetaLucroGlobalPercent is > 0 || cfg.StopPerdaGlobalPercent is > 0,
            UnidadeAlvoGlobal.Absoluto => cfg.MetaLucroGlobalAbsoluto is > 0 || cfg.StopPerdaGlobalAbsoluto is > 0,
            _ => false
        };
    }

    private static TimeSpan ObterIntervaloMinimo(List<ConfiguracaoAlvoGlobalPosicoesAbertas> lista)
    {
        var menorSegundos = lista.Count > 0
            ? lista.Min(c => c.IntervaloVerificacaoSegundos)
            : 10;

        return TimeSpan.FromSeconds(Math.Max(5, menorSegundos));
    }

    private static EstrategiaConfig CriarEstrategiaFallback(SymbolConfig config) => new(
        Id: "cruzamento-ema", Nome: "CruzamentoEma", MagicNumber: 1, Ativa: true,
        Comprar: config.Comprar, Vender: config.Vender, Entrada: config.Entrada,
        Saida: config.Saida, GestaoDeRisco: config.GestaoDeRisco,
        JanelaHorarioPermitido: config.GestaoDeRisco?.JanelaHorarioPermitido,
        Indicadores: config.Indicadores, Parametros: new Dictionary<string, object>());
}
