using Financial.Robot.Domain.Enums;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.Excecoes;
using Financial.Robot.Domain.Primitivos;
using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Domain.Aggregates.Posicao;

/// <summary>
/// Agregado raiz que representa uma posição aberta a mercado no MT5.
/// Encapsula todas as invariantes de negócio relacionadas ao ciclo de vida da posição.
/// </summary>
public sealed class Posicao : AgregadoRaiz<ulong>
{
    /// <summary>Símbolo do ativo negociado.</summary>
    public Simbolo Simbolo { get; private set; } = default!;

    /// <summary>Tipo da ordem (Compra ou Venda).</summary>
    public TipoOrdem TipoOrdem { get; private set; }

    /// <summary>Volume em lotes da posição.</summary>
    public Volume Volume { get; private set; } = default!;

    /// <summary>Preço de abertura da posição.</summary>
    public PrecoMercado PrecoAbertura { get; private set; } = default!;

    /// <summary>Stop Loss atual em preço absoluto (0 = sem SL).</summary>
    public double StopLoss { get; private set; }

    /// <summary>Take Profit atual em preço absoluto (0 = sem TP).</summary>
    public double TakeProfit { get; private set; }

    /// <summary>Status atual da posição.</summary>
    public StatusPosicao Status { get; private set; }

    /// <summary>Momento de abertura da posição.</summary>
    public DateTime AbertaEm { get; private set; }

    /// <summary>Momento de fechamento (nulo se ainda aberta).</summary>
    public DateTime? FechadaEm { get; private set; }

    /// <summary>Lucro/prejuízo da operação após fechamento.</summary>
    public double? LucroPrejuizo { get; private set; }

    private Posicao() { }

    /// <summary>
    /// Cria e abre uma nova posição a mercado, publicando o evento correspondente.
    /// </summary>
    public static Posicao Abrir(
        ulong ticket,
        Simbolo simbolo,
        TipoOrdem tipoOrdem,
        Volume volume,
        PrecoMercado precoAbertura,
        double stopLoss,
        double takeProfit)
    {
        var posicao = new Posicao
        {
            Id = ticket,
            Simbolo = simbolo,
            TipoOrdem = tipoOrdem,
            Volume = volume,
            PrecoAbertura = precoAbertura,
            StopLoss = stopLoss,
            TakeProfit = takeProfit,
            Status = StatusPosicao.Aberta,
            AbertaEm = DateTime.UtcNow
        };

        posicao.PublicarEvento(new PosicaoAbertaEvent(
            ticket,
            simbolo.Valor,
            tipoOrdem.ToString(),
            volume.Valor,
            precoAbertura.Valor,
            stopLoss,
            takeProfit,
            DateTime.UtcNow));

        return posicao;
    }

    /// <summary>
    /// Modifica o Take Profit da posição, mantendo o Stop Loss atual.
    /// </summary>
    public void ModificarTakeProfit(double novoTakeProfit)
    {
        ValidarPosicaoAberta();

        var tpAnterior = TakeProfit;
        var slAtual = StopLoss;

        TakeProfit = novoTakeProfit;

        PublicarEvento(new PosicaoModificadaEvent(
            Id, Simbolo.Valor,
            slAtual, slAtual,
            tpAnterior, novoTakeProfit,
            DateTime.UtcNow));
    }

    /// <summary>
    /// Modifica o Stop Loss da posição, mantendo o Take Profit atual.
    /// </summary>
    public void ModificarStopLoss(double novoStopLoss)
    {
        ValidarPosicaoAberta();

        var slAnterior = StopLoss;
        var tpAtual = TakeProfit;

        StopLoss = novoStopLoss;

        PublicarEvento(new PosicaoModificadaEvent(
            Id, Simbolo.Valor,
            slAnterior, novoStopLoss,
            tpAtual, tpAtual,
            DateTime.UtcNow));
    }

    /// <summary>
    /// Fecha a posição registrando preço de fechamento e resultado financeiro.
    /// </summary>
    public void Fechar(double precoFechamento, double lucroPrejuizo)
    {
        ValidarPosicaoAberta();

        Status = StatusPosicao.Fechada;
        FechadaEm = DateTime.UtcNow;
        LucroPrejuizo = lucroPrejuizo;

        PublicarEvento(new PosicaoFechadaEvent(
            Id, Simbolo.Valor,
            precoFechamento, lucroPrejuizo,
            DateTime.UtcNow));
    }

    private void ValidarPosicaoAberta()
    {
        if (Status != StatusPosicao.Aberta)
            throw new DomainException($"Posição {Id} não está aberta. Status atual: {Status}.");
    }
}
