namespace Financial.Robot.Worker.Extract;

public sealed record SymbolMetadataDto(
    double PointSize,
    double StopsLevel,
    double MinVolume,
    double MaxVolume,
    double VolumeStep);
