using Financial.Robot.Domain.Excecoes;
using Financial.Robot.Domain.Interfaces;

namespace Financial.Robot.Domain.Services;

/// <summary>
/// Domain Service responsável por converter distâncias em pips
/// para preços absolutos de Stop Loss, Take Profit e ordens pendentes.
/// Opera sobre dados de mercado sem pertencer a uma única entidade.
/// </summary>
public sealed class ServicoCalculoPreco
{
    /// <summary>
    /// Calcula o preço absoluto de Stop Loss a partir do preço de abertura e pips.
    /// </summary>
    public double CalcularStopLoss(
        string tipoOrdem,
        double precoAbertura,
        int pips,
        double tamanhoPonto)
    {
        ValidarParametros(precoAbertura, pips, tamanhoPonto);

        var distancia = pips * tamanhoPonto * 10;

        return tipoOrdem.Equals("BUY", StringComparison.OrdinalIgnoreCase)
            ? precoAbertura - distancia
            : precoAbertura + distancia;
    }

    /// <summary>
    /// Calcula o preço absoluto de Take Profit a partir do preço de abertura e pips.
    /// </summary>
    public double CalcularTakeProfit(
        string tipoOrdem,
        double precoAbertura,
        int pips,
        double tamanhoPonto)
    {
        ValidarParametros(precoAbertura, pips, tamanhoPonto);

        var distancia = pips * tamanhoPonto * 10;

        return tipoOrdem.Equals("BUY", StringComparison.OrdinalIgnoreCase)
            ? precoAbertura + distancia
            : precoAbertura - distancia;
    }

    /// <summary>
    /// Calcula o preço de entrada de uma ordem pendente a partir do preço atual e distância em pips.
    /// </summary>
    public double CalcularPrecoOrdemPendente(
        string tipoOrdemPendente,
        double precoAtual,
        int distanciaPips,
        double tamanhoPonto)
    {
        ValidarParametros(precoAtual, distanciaPips, tamanhoPonto);

        var distancia = distanciaPips * tamanhoPonto * 10;

        return tipoOrdemPendente.StartsWith("BUY", StringComparison.OrdinalIgnoreCase)
            ? precoAtual - distancia
            : precoAtual + distancia;
    }

    private static void ValidarParametros(double preco, int pips, double tamanhoPonto)
    {
        if (preco <= 0)
            throw new DomainException($"Preço de referência inválido: {preco}.");
        if (pips <= 0)
            throw new DomainException($"Pips inválido: {pips}.");
        if (tamanhoPonto <= 0)
            throw new DomainException($"Tamanho de ponto inválido: {tamanhoPonto}.");
    }
}
