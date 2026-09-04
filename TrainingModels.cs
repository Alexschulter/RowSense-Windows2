namespace RowSenseWindows.Models;

public sealed class TrainingStroke
{
    public DateTime TimestampUtc { get; set; }
    public int AthleteNumber { get; set; }
    public float[] Forces { get; set; } = Array.Empty<float>();
    public int Tempo { get; set; }
    public float Power { get; set; }
    public float StrokeTime { get; set; }
}

public sealed class TrainingSession
{
    public string Version { get; set; } = "0.1.0";
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public string BoatClass { get; set; } = "1x";
    public double ExternalLeverCm { get; set; }
    public double InternalLeverCm { get; set; }
    public List<TrainingStroke> Strokes { get; set; } = new();
}
