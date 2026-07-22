using Financial.Robot.Application.DTOs;
using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Application.Services;

/// <summary>
/// Application Service responsável por conectar e desconectar do terminal MT5,
/// validando que a conta está acessível após a conexão.
/// </summary>
public sealed class ServicoConexaoMt5 : IServicoConexaoMt5
{
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<ServicoConexaoMt5> _logger;

    private IGatewayMt5 _gateway => _connectionManager.GetClient(_connectionManager.GetConnectedTerminalIds().FirstOrDefault() ?? Financial.Robot.Application.Constants.TerminalDefaults.PrincipalId);

    /// <inheritdoc/>
    public bool EstaConectado => _gateway.EstaConectado;

    /// <summary>Inicializa o serviço com o gerenciador de conexões e logger.</summary>
    public ServicoConexaoMt5(IConnectionManager connectionManager, ILogger<ServicoConexaoMt5> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<InfoContaDto> ConectarAsync(
        string host,
        int porta,
        int timeoutSegundos,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Conectando ao MT5 — Host: {Host}, Porta: {Porta}, Timeout: {Timeout}s",
            host, porta, timeoutSegundos);

        await _gateway.ConectarAsync(host, porta, timeoutSegundos, ct);

        var infoConta = await _gateway.ObterInfoContaAsync(ct);

        _logger.LogInformation(
            "Conectado com sucesso — Conta: {Login} | Titular: {Nome} | Servidor: {Servidor} | Saldo: {Saldo} {Moeda}",
            infoConta.Login, infoConta.NomeTitular, infoConta.Servidor,
            infoConta.Saldo, infoConta.Moeda);

        return new InfoContaDto(
            infoConta.Login,
            infoConta.Servidor,
            infoConta.NomeTitular,
            infoConta.Saldo,
            infoConta.Equidade,
            infoConta.MargemLivre,
            infoConta.Moeda);
    }

    /// <inheritdoc/>
    public async Task DesconectarAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Desconectando do MT5...");
        await _gateway.DesconectarAsync(ct);
        _logger.LogInformation("Desconectado do MT5.");
    }
}
