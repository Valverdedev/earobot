using Financial.Robot.Domain.Enums;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.Excecoes;
using Financial.Robot.Domain.Primitivos;
using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Domain.Aggregates.OrdemPendente;

/// <summary>
/// Agregado raiz que representa uma ordem pendente no MT5.
/// Encapsula o ciclo de vida: criação e cancelamento.
/// </summary>
public sealed class OrdemPendente : AgregadoRaiz<ulong>
{
    /// <summary>Símbolo do ativo.</summary>
    public Simbolo Simbolo { get; private set; } = default!;

    /// <summary>Tipo da ordem pendente.</summary>
    public TipoOrdemPendente TipoOrdem { get; private set; }

    /// <summary>Volume em lotes.</summary>
    public Volume Volume { get; private set; } = default!;

    /// <summary>Preço de entrada definido para a ordem pendente.</summary>
    public PrecoMercado PrecoOrdem { get; private set; } = default!;

    /// <summary>Stop Loss em preço absoluto.</summary>
    public double StopLoss { get; private set; }

    /// <summary>Take Profit em preço absoluto.</summary>
    public double TakeProfit { get; private set; }

    /// <summary>Status atual da ordem pendente.</summary>
    public StatusOrdemPendente Status { get; private set; }

    /// <summary>Momento de criação.</summary>
    public DateTime CriadaEm { get; private set; }

    /// <summary>Momento de cancelamento (nulo se ainda pendente).</summary>
    public DateTime? CanceladaEm { get; private set; }

    private OrdemPendente() { }

    /// <summary>
    /// Cria uma ordem pendente, publicando o evento correspondente.
    /// </summary>
    public static OrdemPendente Criar(
        ulong ticket,
        Simbolo simbolo,
        TipoOrdemPendente tipoOrdem,
        Volume volume,
        PrecoMercado precoOrdem,
        double stopLoss,
        double takeProfit)
    {
        var ordem = new OrdemPendente
        {
            Id = ticket,
            Simbolo = simbolo,
            TipoOrdem = tipoOrdem,
            Volume = volume,
            PrecoOrdem = precoOrdem,
            StopLoss = stopLoss,
            TakeProfit = takeProfit,
            Status = StatusOrdemPendente.Pendente,
            CriadaEm = DateTime.UtcNow
        };

        ordem.PublicarEvento(new OrdemPendenteCriadaEvent(
            ticket,
            simbolo.Valor,
            tipoOrdem.ToString(),
            volume.Valor,
            precoOrdem.Valor,
            stopLoss,
            takeProfit,
            DateTime.UtcNow));

        return ordem;
    }

    /// <summary>
    /// Cancela a ordem pendente, publicando o evento correspondente.
    /// </summary>
    public void Cancelar()
    {
        if (Status != StatusOrdemPendente.Pendente)
            throw new DomainException($"Ordem pendente {Id} não pode ser cancelada. Status: {Status}.");

        Status = StatusOrdemPendente.Cancelada;
        CanceladaEm = DateTime.UtcNow;

        PublicarEvento(new OrdemPendenteCanceladaEvent(
            Id, Simbolo.Valor, DateTime.UtcNow));
    }
}
