using System.IO;
using System.Text.Json;
using RowSenseWindows.Models;

namespace RowSenseWindows.Services;

public sealed class TrainingLogger
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public TrainingSession? Current { get; private set; }

    public static string TrainingsDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "RowSense",
                "Trainings"
            );

            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public void Start(SessionConfig config)
    {
        Current = new TrainingSession
        {
            StartedUtc = DateTime.UtcNow,
            BoatClass = config.BoatClass,
            ExternalLeverCm = config.ExternalLeverCm,
            InternalLeverCm = config.InternalLeverCm
        };
    }

    public void Add(TrainingStroke stroke)
    {
        Current?.Strokes.Add(stroke);
    }

    public string? Finish()
    {
        if (Current is null)
            return null;

        Current.FinishedUtc = DateTime.UtcNow;

        var file = Path.Combine(
            TrainingsDirectory,
            $"training_{Current.StartedUtc:yyyyMMdd_HHmmss}.json"
        );

        File.WriteAllText(
            file,
            JsonSerializer.Serialize(Current, _options)
        );

        Current = null;
        return file;
    }
}
