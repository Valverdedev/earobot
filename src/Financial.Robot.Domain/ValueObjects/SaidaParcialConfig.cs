namespace Financial.Robot.Domain.ValueObjects;

/// <summary>
/// Um alvo de saída parcial: quando o lucro (em preço, a partir da entrada) atinge
/// <see cref="DistanciaPreco"/>, fecha-se a fração <see cref="PercentualVolume"/> do volume
/// original da posição. Os alvos devem ser configurados em ordem crescente de distância.
/// </summary>
public sealed record SaidaParcialConfig(
    double PercentualVolume,
    decimal DistanciaPreco
);
