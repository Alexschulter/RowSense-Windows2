namespace RowSenseWindows.Models;

public sealed class SessionConfig
{
    public string BoatClass { get; set; } = "1x";
    public double ExternalLeverCm { get; set; } = 260.0;
    public double InternalLeverCm { get; set; } = 116.0;

    public int AthleteCount => BoatClass switch
    {
        "8+" => 8,
        "4x" or "4-" or "4+" => 4,
        "2x" or "2-" => 2,
        _ => 1
    };

    public double ForceCorrection => (ExternalLeverCm / InternalLeverCm) / (260.0 / 116.0);
    public double PowerCorrection => ExternalLeverCm / 260.0;
}
