using System.Diagnostics;
using System.Text.Json;
using Financial.Robot.Application.Configuracoes;
using Financial.Robot.Application.DTOs;
using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Aggregates.OrdemPendente;
using Financial.Robot.Domain.Aggregates.Posicao;
using Financial.Robot.Domain.Excecoes;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.Services;
using Financial.Robot.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Financial.Robot.Application.Services;

/// <summary>
/// Orquestra a sequência completa de 14 passos do Modo Teste MT5.
/// Cada passo é logado antes (intenção) e depois (resultado).
/// Interrompe na primeira falha com resumo estruturado.
/// </summary>
public sealed class ServicoModoTeste : IServicoModoTeste
{
    private readonly IConnectionManager _connectionManager;
    private readonly IRepositorioPosicao _repositorioPosicao;
    private readonly IRepositorioOrdemPendente _repositorioOrdemPendente;
    private readonly ServicoCalculoPreco _servicoCalculo;
    private readonly ConfiguracaoModoTeste _config;
    private readonly ILogger<ServicoModoTeste> _logger;

    private IGatewayMt5 _gateway => _connectionManager.GetClient(_connectionManager.GetConnectedTerminalIds().FirstOrDefault() ?? Financial.Robot.Application.Constants.TerminalDefaults.PrincipalId);

    private readonly List<ResultadoPassoDto> _passos = [];

    /// <summary>Inicializa o serviço com todas as dependências necessárias.</summary>
    public ServicoModoTeste(
        IConnectionManager connectionManager,
        IRepositorioPosicao repositorioPosicao,
        IRepositorioOrdemPendente repositorioOrdemPendente,
        ServicoCalculoPreco servicoCalculo,
        IOptions<ConfiguracaoModoTeste> config,
        ILogger<ServicoModoTeste> logger)
    {
        _connectionManager = connectionManager;
        _repositorioPosicao = repositorioPosicao;
        _repositorioOrdemPendente = repositorioOrdemPendente;
        _servicoCalculo = servicoCalculo;
        _config = config.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ResumoTesteDto> ExecutarAsync(CancellationToken ct = default)
    {
        var inicio = DateTime.UtcNow;
        var seq = _config.SequenciaTeste;
        ulong ticketPosicao = 0;
        ulong ticketOrdemPendente = 0;

        _logger.LogInformation("=== MODO TESTE INICIADO === Símbolo: {Simbolo}, Volume: {Volume}",
            seq.Simbolo, seq.Volume);

        // ─── Passo 1: Conectar ──────────────────────────────────────────────────
        if (!await ExecutarPassoAsync(1, "Conectar ao MetaTrader",
            () => ExecutarConexaoAsync(seq, ct), ct))
        {
            return CriarResumo(false, 1, "Falha na conexão com o MT5", inicio);
        }

        // ─── Passo 2: Abrir ordem a mercado ─────────────────────────────────────
        ulong? ticket = null;
        if (!await ExecutarPassoAsync(2, "Abrir ordem a mercado",
            async () => ticket = await AbrirOrdemAsync(seq, ct), ct))
        {
            return await FinalizarComErro(2, "Falha ao abrir ordem", inicio, ct);
        }
        ticketPosicao = ticket!.Value;

        // ─── Passo 3: Delay ──────────────────────────────────────────────────────
        await AguardarDelayAsync(3, seq.DelayEntreAcoesSegundos, ct);

        // ─── Passo 4: Alterar Take Profit ────────────────────────────────────────
        if (!await ExecutarPassoAsync(4, "Alterar Take Profit",
            () => AlterarTakeProfitAsync(ticketPosicao, seq, ct), ct))
        {
            return await FinalizarComErro(4, "Falha ao alterar Take Profit", inicio, ct);
        }

        // ─── Passo 5: Delay ──────────────────────────────────────────────────────
        await AguardarDelayAsync(5, seq.DelayEntreAcoesSegundos, ct);

        // ─── Passo 6: Alterar Stop Loss ─────────────────────────────────────────
        if (!await ExecutarPassoAsync(6, "Alterar Stop Loss",
            () => AlterarStopLossAsync(ticketPosicao, seq, ct), ct))
        {
            return await FinalizarComErro(6, "Falha ao alterar Stop Loss", inicio, ct);
        }

        // ─── Passo 7: Delay ──────────────────────────────────────────────────────
        await AguardarDelayAsync(7, seq.DelayEntreAcoesSegundos, ct);

        // ─── Passo 8: Fechar posição ─────────────────────────────────────────────
        if (!await ExecutarPassoAsync(8, "Fechar posição",
            () => FecharPosicaoAsync(ticketPosicao, ct), ct))
        {
            return await FinalizarComErro(8, "Falha ao fechar posição", inicio, ct);
        }

        // ─── Passo 9: Delay ──────────────────────────────────────────────────────
        await AguardarDelayAsync(9, seq.DelayEntreAcoesSegundos, ct);

        // ─── Passo 10: Criar ordem pendente ─────────────────────────────────────
        ulong? ticketPendente = null;
        if (!await ExecutarPassoAsync(10, "Criar ordem pendente",
            async () => ticketPendente = await CriarOrdemPendenteAsync(seq, ct), ct))
        {
            return await FinalizarComErro(10, "Falha ao criar ordem pendente", inicio, ct);
        }
        ticketOrdemPendente = ticketPendente!.Value;

        // ─── Passo 11: Delay ─────────────────────────────────────────────────────
        await AguardarDelayAsync(11, seq.DelayEntreAcoesSegundos, ct);

        // ─── Passo 12: Cancelar ordem pendente ──────────────────────────────────
        if (!await ExecutarPassoAsync(12, "Cancelar ordem pendente",
            () => CancelarOrdemPendenteAsync(ticketOrdemPendente, ct), ct))
        {
            return await FinalizarComErro(12, "Falha ao cancelar ordem pendente", inicio, ct);
        }

        // ─── Passo 13: Delay ─────────────────────────────────────────────────────
        await AguardarDelayAsync(13, seq.DelayEntreAcoesSegundos, ct);

        // ─── Passo 14: Desconectar ───────────────────────────────────────────────
        await ExecutarPassoAsync(14, "Desconectar do MetaTrader",
            async () => await _gateway.DesconectarAsync(ct), ct);

        var resumo = CriarResumo(true, null, null, inicio);
        LogarResumoFinal(resumo);
        return resumo;
    }

    // ─── Métodos de cada passo ──────────────────────────────────────────────────

    private async Task ExecutarConexaoAsync(ConfiguracaoSequenciaTeste seq, CancellationToken ct)
    {
        await _gateway.ConectarAsync(
            _config.MtApi.Host,
            _config.MtApi.Porta,
            _config.MtApi.TimeoutConexaoSegundos,
            ct);

        var infoConta = await _gateway.ObterInfoContaAsync(ct);
        _logger.LogInformation(
            "Conta conectada — Login: {Login} | Titular: {Nome} | Saldo: {Saldo} {Moeda}",
            infoConta.Login, infoConta.NomeTitular, infoConta.Saldo, infoConta.Moeda);
    }

    private async Task<ulong> AbrirOrdemAsync(ConfiguracaoSequenciaTeste seq, CancellationToken ct)
    {
        var tick = await _gateway.ObterTickAtualAsync(seq.Simbolo, ct);
        var tamanhoPonto = await _gateway.ObterTamanhoPontoAsync(seq.Simbolo, ct);
        var precoRef = seq.TipoOrdem.Equals("BUY", StringComparison.OrdinalIgnoreCase)
            ? tick.Ask : tick.Bid;

        var sl = _servicoCalculo.CalcularStopLoss(seq.TipoOrdem, precoRef, seq.StopLossPips, tamanhoPonto);
        var tp = _servicoCalculo.CalcularTakeProfit(seq.TipoOrdem, precoRef, seq.TakeProfitPips, tamanhoPonto);

        _logger.LogInformation(
            "Passo 2 — Enviando ordem: {Tipo} {Simbolo} Vol={Volume} SL={SL:F5} TP={TP:F5}",
            seq.TipoOrdem, seq.Simbolo, seq.Volume, sl, tp);

        var resultado = await _gateway.AbrirOrdemMercadoAsync(
            seq.Simbolo, seq.TipoOrdem, seq.Volume, sl, tp,
            "financial.robot.test", 999, ct);

        if (!resultado.Sucesso)
            throw new OrdemInvalidaException(
                $"Falha ao abrir ordem: {resultado.MensagemErro}", resultado.CodigoRetorno);

        var posicao = Posicao.Abrir(
            resultado.Ticket,
            new Domain.ValueObjects.Simbolo(seq.Simbolo),
            seq.TipoOrdem.Equals("BUY", StringComparison.OrdinalIgnoreCase)
                ? Domain.Enums.TipoOrdem.Compra : Domain.Enums.TipoOrdem.Venda,
            new Volume(seq.Volume),
            new PrecoMercado(resultado.PrecoExecutado),
            sl, tp);

        await _repositorioPosicao.AdicionarAsync(posicao, ct);

        _logger.LogInformation(
            "Passo 2 ✓ — Ticket: {Ticket} | Preço abertura: {Preco:F5}",
            resultado.Ticket, resultado.PrecoExecutado);

        return resultado.Ticket;
    }

    private async Task AlterarTakeProfitAsync(ulong ticket, ConfiguracaoSequenciaTeste seq, CancellationToken ct)
    {
        var posicao = await _repositorioPosicao.ObterPorTicketAsync(ticket, ct)
            ?? throw new DomainException($"Posição {ticket} não encontrada.");

        var tamanhoPonto = await _gateway.ObterTamanhoPontoAsync(seq.Simbolo, ct);
        var novoTp = _servicoCalculo.CalcularTakeProfit(
            seq.TipoOrdem, posicao.PrecoAbertura.Valor, seq.TakeProfitModificadoPips, tamanhoPonto);

        _logger.LogInformation(
            "Passo 4 — Modificando TP: Ticket={Ticket} TP antigo={TpAnterior:F5} → TP novo={TpNovo:F5}",
            ticket, posicao.TakeProfit, novoTp);

        var sucesso = await _gateway.ModificarPosicaoAsync(ticket, posicao.StopLoss, novoTp, ct);
        if (!sucesso)
            throw new DomainException($"Falha ao modificar TP da posição {ticket}.");

        posicao.ModificarTakeProfit(novoTp);
        await _repositorioPosicao.AtualizarAsync(posicao, ct);

        _logger.LogInformation("Passo 4 ✓ — TP modificado para {TpNovo:F5}", novoTp);
    }

    private async Task AlterarStopLossAsync(ulong ticket, ConfiguracaoSequenciaTeste seq, CancellationToken ct)
    {
        var posicao = await _repositorioPosicao.ObterPorTicketAsync(ticket, ct)
            ?? throw new DomainException($"Posição {ticket} não encontrada.");

        var tamanhoPonto = await _gateway.ObterTamanhoPontoAsync(seq.Simbolo, ct);
        var novoSl = _servicoCalculo.CalcularStopLoss(
            seq.TipoOrdem, posicao.PrecoAbertura.Valor, seq.StopLossModificadoPips, tamanhoPonto);

        _logger.LogInformation(
            "Passo 6 — Modificando SL: Ticket={Ticket} SL antigo={SlAnterior:F5} → SL novo={SlNovo:F5}",
            ticket, posicao.StopLoss, novoSl);

        var sucesso = await _gateway.ModificarPosicaoAsync(ticket, novoSl, posicao.TakeProfit, ct);
        if (!sucesso)
            throw new DomainException($"Falha ao modificar SL da posição {ticket}.");

        posicao.ModificarStopLoss(novoSl);
        await _repositorioPosicao.AtualizarAsync(posicao, ct);

        _logger.LogInformation("Passo 6 ✓ — SL modificado para {SlNovo:F5}", novoSl);
    }

    private async Task FecharPosicaoAsync(ulong ticket, CancellationToken ct)
    {
        var posicao = await _repositorioPosicao.ObterPorTicketAsync(ticket, ct)
            ?? throw new DomainException($"Posição {ticket} não encontrada.");

        _logger.LogInformation("Passo 8 — Fechando posição: Ticket={Ticket}", ticket);

        var resultado = await _gateway.FecharPosicaoAsync(ticket, ct);
        if (!resultado.Sucesso)
            throw new OrdemInvalidaException(
                $"Falha ao fechar posição {ticket}: {resultado.MensagemErro}",
                resultado.CodigoRetorno);

        posicao.Fechar(resultado.PrecoFechamento, resultado.LucroPrejuizo);
        await _repositorioPosicao.AtualizarAsync(posicao, ct);

        _logger.LogInformation(
            "Passo 8 ✓ — Posição fechada | Preço: {Preco:F5} | L/P: {LP:F2}",
            resultado.PrecoFechamento, resultado.LucroPrejuizo);
    }

    private async Task<ulong> CriarOrdemPendenteAsync(ConfiguracaoSequenciaTeste seq, CancellationToken ct)
    {
        var op = seq.OrdemPendente;
        var tick = await _gateway.ObterTickAtualAsync(seq.Simbolo, ct);
        var tamanhoPonto = await _gateway.ObterTamanhoPontoAsync(seq.Simbolo, ct);
        var precoOrdem = _servicoCalculo.CalcularPrecoOrdemPendente(
            op.Tipo, tick.Bid, op.DistanciaPips, tamanhoPonto);
        var sl = _servicoCalculo.CalcularStopLoss(
            op.Tipo.StartsWith("BUY") ? "BUY" : "SELL", precoOrdem, op.StopLossPips, tamanhoPonto);
        var tp = _servicoCalculo.CalcularTakeProfit(
            op.Tipo.StartsWith("BUY") ? "BUY" : "SELL", precoOrdem, op.TakeProfitPips, tamanhoPonto);

        _logger.LogInformation(
            "Passo 10 — Criando ordem pendente: {Tipo} {Simbolo} Preço={Preco:F5} SL={SL:F5} TP={TP:F5}",
            op.Tipo, seq.Simbolo, precoOrdem, sl, tp);

        var resultado = await _gateway.CriarOrdemPendenteAsync(
            seq.Simbolo, op.Tipo, seq.Volume, precoOrdem, sl, tp, 999, ct);

        if (!resultado.Sucesso)
            throw new OrdemInvalidaException(
                $"Falha ao criar ordem pendente: {resultado.MensagemErro}", resultado.CodigoRetorno);

        var tipoOrdemPendente = op.Tipo switch
        {
            "BUY_LIMIT" => Domain.Enums.TipoOrdemPendente.CompraLimite,
            "SELL_LIMIT" => Domain.Enums.TipoOrdemPendente.VendaLimite,
            "BUY_STOP" => Domain.Enums.TipoOrdemPendente.CompraStop,
            _ => Domain.Enums.TipoOrdemPendente.VendaStop
        };

        var ordemPendente = OrdemPendente.Criar(
            resultado.Ticket,
            new Domain.ValueObjects.Simbolo(seq.Simbolo),
            tipoOrdemPendente,
            new Volume(seq.Volume),
            new PrecoMercado(precoOrdem),
            sl, tp);

        await _repositorioOrdemPendente.AdicionarAsync(ordemPendente, ct);

        _logger.LogInformation(
            "Passo 10 ✓ — Ordem pendente criada | Ticket: {Ticket} | Preço: {Preco:F5}",
            resultado.Ticket, precoOrdem);

        return resultado.Ticket;
    }

    private async Task CancelarOrdemPendenteAsync(ulong ticket, CancellationToken ct)
    {
        var ordemPendente = await _repositorioOrdemPendente.ObterPorTicketAsync(ticket, ct)
            ?? throw new DomainException($"Ordem pendente {ticket} não encontrada.");

        _logger.LogInformation("Passo 12 — Cancelando ordem pendente: Ticket={Ticket}", ticket);

        var sucesso = await _gateway.CancelarOrdemPendenteAsync(ticket, ct);
        if (!sucesso)
            throw new DomainException($"Falha ao cancelar ordem pendente {ticket}.");

        ordemPendente.Cancelar();
        await _repositorioOrdemPendente.AtualizarAsync(ordemPendente, ct);

        _logger.LogInformation("Passo 12 ✓ — Ordem pendente {Ticket} cancelada.", ticket);
    }

    // ─── Infraestrutura de execução e logging ───────────────────────────────────

    private async Task<bool> ExecutarPassoAsync(
        int numero,
        string nome,
        Func<Task> acao,
        CancellationToken ct)
    {
        var inicio = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("→ Passo {Numero}: {Nome}", numero, nome);

        try
        {
            await acao();
            sw.Stop();
            _passos.Add(new ResultadoPassoDto(numero, nome, true, null, inicio, sw.Elapsed));
            return true;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "✗ Passo {Numero} FALHOU: {Mensagem}", numero, ex.Message);
            _passos.Add(new ResultadoPassoDto(numero, nome, false, ex.Message, inicio, sw.Elapsed));
            return false;
        }
    }

    private async Task AguardarDelayAsync(int numeroPasso, int segundos, CancellationToken ct)
    {
        _logger.LogInformation("Passo {Numero}: Aguardando {Segundos}s...", numeroPasso, segundos);
        await Task.Delay(TimeSpan.FromSeconds(segundos), ct);
        _passos.Add(new ResultadoPassoDto(
            numeroPasso, $"Delay {segundos}s", true, null, DateTime.UtcNow, TimeSpan.FromSeconds(segundos)));
    }

    private async Task<ResumoTesteDto> FinalizarComErro(
        int passoParada,
        string motivo,
        DateTime inicio,
        CancellationToken ct)
    {
        _logger.LogWarning("Tentando desconectar após falha no passo {Passo}...", passoParada);
        try { await _gateway.DesconectarAsync(ct); } catch { /* ignora erro de desconexão */ }

        var resumo = CriarResumo(false, passoParada, motivo, inicio);
        LogarResumoFinal(resumo);
        return resumo;
    }

    private ResumoTesteDto CriarResumo(
        bool sucesso,
        int? passoParada,
        string? motivo,
        DateTime inicio)
    {
        var fim = DateTime.UtcNow;
        return new ResumoTesteDto(
            sucesso,
            14,
            _passos.Count(p => p.Sucesso),
            passoParada,
            motivo,
            _passos.AsReadOnly(),
            inicio,
            fim,
            fim - inicio);
    }

    private void LogarResumoFinal(ResumoTesteDto resumo)
    {
        var json = JsonSerializer.Serialize(resumo, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (resumo.Sucesso)
            _logger.LogInformation("=== RESUMO FINAL (SUCESSO) ===\n{Resumo}", json);
        else
            _logger.LogError("=== RESUMO FINAL (FALHA no passo {Passo}) ===\n{Resumo}",
                resumo.PassoParada, json);
    }
}
