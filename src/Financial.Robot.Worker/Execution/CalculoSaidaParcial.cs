using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Worker.Execution;

/// <summary>
/// Funções puras de cálculo para saídas parciais (scale-out), extraídas do
/// <see cref="GerenciadorPosicoesAbertasService"/> para permitir teste unitário direto.
/// </summary>
public static class CalculoSaidaParcial
{
    /// <summary>Lucro atual em preço (positivo = a favor da posição).</summary>
    public static double CalcularLucroAtual(bool compra, double precoAbertura, double precoAtual) =>
        compra ? precoAtual - precoAbertura : precoAbertura - precoAtual;

    /// <summary>
    /// Volume a fechar na saída parcial de índice <paramref name="indice"/>, normalizado às regras
    /// de volume do símbolo (min/step). Reconstrói o volume original da posição a partir do volume
    /// atual e da soma dos percentuais já consumidos pelas parciais anteriores. Se o volume restante
    /// após a normalização for menor que o mínimo negociável, ou se o alvo cobrir o volume inteiro,
    /// fecha a posição inteira nesta parcial (evita deixar um resíduo abaixo do lote mínimo).
    /// </summary>
    public static double CalcularVolumeParcial(
        double volumeAtual,
        IReadOnlyList<SaidaParcialConfig> parciais,
        int indice,
        (double MinVolume, double MaxVolume, double VolumeStep) regrasVolume)
    {
        var alvo = parciais[indice];
        var volumeInicialEstimado = volumeAtual / (1.0 - SomaPercentuaisAnteriores(parciais, indice));
        var volumeAlvo = volumeInicialEstimado * alvo.PercentualVolume;
        var volumeFechar = NormalizarVolume(Math.Min(volumeAlvo, volumeAtual), regrasVolume);

        if (volumeFechar <= 0 || volumeFechar >= volumeAtual)
            return volumeAtual;

        return volumeFechar;
    }

    /// <summary>
    /// Nível de break-even a aplicar após a saída parcial de índice <paramref name="indiceExecutado"/>:
    /// preço de entrada após a 1ª parcial, e a distância da parcial anterior a partir da 2ª em diante
    /// (mesma regra do "Break Even Financeiro" do ebook de referência: o breakeven avança a cada saída).
    /// </summary>
    public static double CalcularBreakEvenAposParcial(
        bool compra, double precoAbertura, IReadOnlyList<SaidaParcialConfig> parciais, int indiceExecutado)
    {
        if (indiceExecutado == 0) return precoAbertura;

        var distanciaAnterior = (double)parciais[indiceExecutado - 1].DistanciaPreco;
        return compra ? precoAbertura + distanciaAnterior : precoAbertura - distanciaAnterior;
    }

    private static double SomaPercentuaisAnteriores(IReadOnlyList<SaidaParcialConfig> parciais, int ateIndiceExclusivo)
    {
        double soma = 0;
        for (var i = 0; i < ateIndiceExclusivo; i++)
            soma += parciais[i].PercentualVolume;
        return Math.Clamp(soma, 0, 0.999);
    }

    private static double NormalizarVolume(double volume, (double MinVolume, double MaxVolume, double VolumeStep) regras)
    {
        var step = regras.VolumeStep > 0 ? regras.VolumeStep : 0.01;
        var minLot = regras.MinVolume > 0 ? regras.MinVolume : 0.01;
        var maxLot = regras.MaxVolume > 0 ? regras.MaxVolume : double.MaxValue;

        var normalizado = Math.Round(volume / step) * step;
        normalizado = Math.Min(normalizado, maxLot);
        if (normalizado < minLot) return 0;

        var decimais = Math.Max(0, (int)Math.Ceiling(-Math.Log10(step)));
        return Math.Round(normalizado, decimais);
    }
}
