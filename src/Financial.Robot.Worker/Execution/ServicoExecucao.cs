using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Execution;

/// <summary>
/// Implementação do serviço de execução de ordens, delegando para o gateway MT5.
/// Não lança exceções para o chamador — retorna ResultadoOrdem com contexto de erro.
/// </summary>
public sealed class ServicoExecucao : IServicoExecucao
{
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<ServicoExecucao> _logger;

    public ServicoExecucao(IConnectionManager connectionManager, ILogger<ServicoExecucao> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ResultadoOrdem> AbrirPosicaoAsync(
        string terminalId, string simbolo, LadoOrdem lado, double volume,
        double? sl, double? tp, long magicNumber = 0, string comentario = "financial.robot", CancellationToken ct = default)
    {
        try
        {
            if (!sl.HasValue || sl.Value <= 0)
            {
                _logger.LogWarning(
                    "[ExecuÃ§Ã£o] Ordem bloqueada: Stop Loss obrigatÃ³rio ausente ou invÃ¡lido para {Simbolo}. Terminal={Terminal}",
                    simbolo, terminalId);
                return ResultadoOrdem.Falha("Stop Loss obrigatório ausente ou inválido.");
            }

            var gateway = _connectionManager.GetClient(terminalId);
            var tipoOrdem = lado == LadoOrdem.Compra ? "BUY" : "SELL";

            _logger.LogInformation(
                "[Execução] Abrindo {Lado} {Simbolo} | Vol={Volume} SL={SL} TP={TP} | Terminal={Terminal}",
                tipoOrdem, simbolo, volume, sl, tp, terminalId);

            var resultado = await gateway.AbrirOrdemMercadoAsync(
                simbolo, tipoOrdem, volume,
                sl ?? 0, tp ?? 0,
                comentario, magicNumber, ct);

            if (resultado.Sucesso)
            {
                _logger.LogInformation(
                    "[Execução] ✅ Ordem aberta — Ticket={Ticket} Preço={Preco} Retcode={Retcode}",
                    resultado.Ticket, resultado.PrecoExecutado, resultado.CodigoRetorno);
                return ResultadoOrdem.Ok(resultado.Ticket);
            }
            else
            {
                _logger.LogWarning(
                    "[Execução] ❌ Ordem recusada — Retcode={Retcode} Msg={Msg}",
                    resultado.CodigoRetorno, resultado.MensagemErro);
                return ResultadoOrdem.Falha($"Retcode={resultado.CodigoRetorno}: {resultado.MensagemErro}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Execução] Erro ao abrir posição em {Simbolo}.", simbolo);
            return ResultadoOrdem.Falha(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<ResultadoOrdem> FecharPosicaoAsync(
        string terminalId, ulong ticket, CancellationToken ct = default)
    {
        try
        {
            var gateway = _connectionManager.GetClient(terminalId);

            _logger.LogInformation("[Execução] Fechando posição Ticket={Ticket} | Terminal={Terminal}", ticket, terminalId);

            var resultado = await gateway.FecharPosicaoAsync(ticket, ct);

            if (resultado.Sucesso)
            {
                _logger.LogInformation(
                    "[Execução] ✅ Posição fechada — Ticket={Ticket} PnL={PnL:F2} Preço={Preco}",
                    ticket, resultado.LucroPrejuizo, resultado.PrecoFechamento);
                return ResultadoOrdem.Ok(ticket);
            }
            else
            {
                _logger.LogWarning("[Execução] ❌ Falha ao fechar — Retcode={Retcode} Msg={Msg}", resultado.CodigoRetorno, resultado.MensagemErro);
                return ResultadoOrdem.Falha($"Retcode={resultado.CodigoRetorno}: {resultado.MensagemErro}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Execução] Erro ao fechar posição Ticket={Ticket}.", ticket);
            return ResultadoOrdem.Falha(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<ResultadoOrdem> FecharPosicaoParcialAsync(
        string terminalId, ulong ticket, double volume, CancellationToken ct = default)
    {
        try
        {
            var gateway = _connectionManager.GetClient(terminalId);

            _logger.LogInformation("[Execução] Fechando parcial Ticket={Ticket} Volume={Volume} | Terminal={Terminal}", ticket, volume, terminalId);

            var resultado = await gateway.FecharPosicaoParcialAsync(ticket, volume, ct);

            if (resultado.Sucesso)
            {
                _logger.LogInformation("[Execução] ✅ Parcial executada — Ticket={Ticket} Volume={Volume} Preço={Preco}", ticket, volume, resultado.PrecoFechamento);
                return ResultadoOrdem.Ok(ticket);
            }
            else
            {
                _logger.LogWarning("[Execução] ❌ Falha ao fechar parcial — Ticket={Ticket} Msg={Msg}", ticket, resultado.MensagemErro);
                return ResultadoOrdem.Falha(resultado.MensagemErro ?? "Falha desconhecida ao fechar parcial.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Execução] Erro ao fechar parcial Ticket={Ticket}.", ticket);
            return ResultadoOrdem.Falha(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<ResultadoOrdem> ModificarPosicaoAsync(
        string terminalId, ulong ticket, double? sl, double? tp, CancellationToken ct = default)
    {
        try
        {
            var gateway = _connectionManager.GetClient(terminalId);

            _logger.LogInformation("[Execução] Modificando Ticket={Ticket} SL={SL} TP={TP}", ticket, sl, tp);

            var ok = await gateway.ModificarPosicaoAsync(ticket, sl ?? 0, tp ?? 0, ct);
            return ok ? ResultadoOrdem.Ok(ticket) : ResultadoOrdem.Falha("ModificarPosicao retornou false");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Execução] Erro ao modificar posição Ticket={Ticket}.", ticket);
            return ResultadoOrdem.Falha(ex.Message);
        }
    }
}
