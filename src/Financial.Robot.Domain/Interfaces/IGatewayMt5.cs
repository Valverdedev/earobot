using Financial.Robot.Domain.Aggregates.Posicao;
using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Domain.Interfaces;

/// <summary>
/// Contrato do Gateway de comunicação com o terminal MetaTrader 5.
/// Interface definida no Domain — implementada na Infrastructure.
/// </summary>
public interface IGatewayMt5
{
    /// <summary>Conecta ao terminal MT5 na porta especificada com timeout.</summary>
    Task ConectarAsync(string host, int porta, int timeoutSegundos, CancellationToken ct = default);

    /// <summary>Desconecta do terminal MT5.</summary>
    Task DesconectarAsync(CancellationToken ct = default);

    /// <summary>Evento disparado quando um novo tick é recebido.</summary>
    event EventHandler<TickMt5>? OnTick;

    /// <summary>Verifica se a conexão está ativa.</summary>
    bool EstaConectado { get; }

    /// <summary>Obtém informações da conta conectada (saldo, equity, nome).</summary>
    Task<InfoContaMt5> ObterInfoContaAsync(CancellationToken ct = default);

    /// <summary>Obtém o tick atual (bid/ask) de um símbolo.</summary>
    Task<TickMt5> ObterTickAtualAsync(string simbolo, CancellationToken ct = default);

    /// <summary>Obtém o tamanho do ponto (point) de um símbolo para cálculo de pips.</summary>
    Task<double> ObterTamanhoPontoAsync(string simbolo, CancellationToken ct = default);
    
    /// <summary>Obtém a distância de Stops Level (em pontos) para o símbolo.</summary>
    Task<double> ObterStopsLevelAsync(string simbolo, CancellationToken ct = default);

    /// <summary>Obtém os limites de volume (mínimo, máximo e step) para o símbolo.</summary>
    Task<(double MinVolume, double MaxVolume, double VolumeStep)> ObterRegrasVolumeAsync(string simbolo, CancellationToken ct = default);

    /// <summary>Busca candles históricos (CopyRates) para um símbolo e timeframe.</summary>
    Task<IReadOnlyList<CandleMt5>> ObterCandlesAsync(string simbolo, string timeframe, int quantidade, CancellationToken ct = default);

    /// <summary>Subscreve o símbolo no Market Watch do terminal (necessário para receber QuoteUpdate).</summary>
    Task<bool> SubscreverSimboloAsync(string simbolo, CancellationToken ct = default);

    /// <summary>Obtém a lista de tickets das posições abertas para o símbolo.</summary>
    Task<IReadOnlyList<ulong>> ObterTicketsPosicoesAbertasAsync(string simbolo, long? magicNumber = null, CancellationToken ct = default);

    /// <summary>Calcula os stops consecutivos atuais para uma estratégia com base nas posições fechadas recentes.</summary>
    Task<int> ObterStopsConsecutivosAsync(string simbolo, long magicNumber, CancellationToken ct = default);

    /// <summary>Obtém detalhes completos das posições abertas para um símbolo (e opcionalmente magic number).</summary>
    Task<IReadOnlyList<DetalhesPosicaoMt5>> ObterDetalhesPosicoesAbertasAsync(
        string simbolo, long? magicNumber = null, CancellationToken ct = default);

    /// <summary>
    /// Obtém o lucro/prejuízo fechado no dia para um dado símbolo e magic number.
    /// </summary>
    Task<double> ObterLucroPrejuizoDiaAsync(string simbolo, long magicNumber, DateTime inicioDia, CancellationToken ct = default);

    /// <summary>
    /// Obtém o lucro/prejuízo não realizado (aberto) atual para um dado símbolo e magic number.
    /// </summary>
    Task<double> ObterLucroAbertoAsync(string simbolo, long magicNumber, CancellationToken ct = default);

    /// <summary>
    /// Obtém o horário (UTC) em que a última posição (deal de saída) foi fechada para o símbolo e magic number, nas últimas 24h.
    /// </summary>
    Task<DateTime?> ObterMomentoUltimoFechamentoAsync(string simbolo, long magicNumber, CancellationToken ct = default);

    /// <summary>
    /// Obtém deals históricos do MT5 para auditoria de entradas e saídas.
    /// </summary>
    Task<IReadOnlyList<DealMt5>> ObterDealsHistoricosAsync(
        string simbolo,
        long? magicNumber,
        DateTime inicioUtc,
        DateTime fimUtc,
        CancellationToken ct = default);

    /// <summary>Abre uma ordem a mercado (compra ou venda).</summary>
    Task<ResultadoOrdemMt5> AbrirOrdemMercadoAsync(
        string simbolo,
        string tipoOrdem,
        double volume,
        double stopLoss,
        double takeProfit,
        string comentario,
        long magicNumber,
        CancellationToken ct = default);

    /// <summary>Modifica Stop Loss e Take Profit de uma posição aberta.</summary>
    Task<bool> ModificarPosicaoAsync(
        ulong ticket,
        double novoStopLoss,
        double novoTakeProfit,
        CancellationToken ct = default);

    /// <summary>Fecha uma posição aberta pelo ticket.</summary>
    Task<ResultadoFechamentoMt5> FecharPosicaoAsync(
        ulong ticket,
        CancellationToken ct = default);

    /// <summary>Fecha parcialmente uma posição aberta, reduzindo seu volume pelo valor informado.</summary>
    Task<ResultadoFechamentoMt5> FecharPosicaoParcialAsync(
        ulong ticket,
        double volume,
        CancellationToken ct = default);

    /// <summary>Cria uma ordem pendente (Limit ou Stop).</summary>
    Task<ResultadoOrdemMt5> CriarOrdemPendenteAsync(
        string simbolo,
        string tipoOrdem,
        double volume,
        double precoOrdem,
        double stopLoss,
        double takeProfit,
        long magicNumber,
        CancellationToken ct = default);

    /// <summary>Cancela uma ordem pendente pelo ticket.</summary>
    Task<bool> CancelarOrdemPendenteAsync(
        ulong ticket,
        CancellationToken ct = default);
}

/// <summary>Dados da conta retornados pelo MT5.</summary>
public sealed record InfoContaMt5(
    long Login,
    string Servidor,
    string NomeTitular,
    double Saldo,
    double Equidade,
    double MargemLivre,
    string Moeda);

/// <summary>Tick de mercado retornado pelo MT5.</summary>
public sealed record TickMt5(
    string Simbolo,
    double Bid,
    double Ask,
    DateTime Tempo);

/// <summary>Resultado de uma ordem enviada ao MT5.</summary>
public sealed record ResultadoOrdemMt5(
    bool Sucesso,
    ulong Ticket,
    double PrecoExecutado,
    double VolumeExecutado,
    int CodigoRetorno,
    string? MensagemErro = null);

/// <summary>Resultado de fechamento de posição.</summary>
public sealed record ResultadoFechamentoMt5(
    bool Sucesso,
    double PrecoFechamento,
    double LucroPrejuizo,
    int CodigoRetorno,
    string? MensagemErro = null);

/// <summary>Detalhes completos de uma posição aberta, usados para gestão dinâmica (breakeven/trailing).</summary>
public sealed record DetalhesPosicaoMt5(
    ulong Ticket,
    string Simbolo,
    long MagicNumber,
    bool Compra,
    double PrecoAbertura,
    double PrecoAtual,
    double StopLossAtual,
    double TakeProfitAtual,
    double Volume,
    double LucroBruto);

/// <summary>Deal histórico retornado pelo MT5 para auditoria.</summary>
public sealed record DealMt5(
    ulong Ticket,
    ulong Order,
    long PositionId,
    string Simbolo,
    long MagicNumber,
    DateTime TimeUtc,
    string Type,
    string Entry,
    double Price,
    double Volume,
    double Profit,
    double Commission,
    double Swap,
    string? Comment);
