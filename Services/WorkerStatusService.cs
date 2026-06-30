using System.Text.Json;
using PersonValidationService.Models;

namespace PersonValidationService.Services;

public sealed class WorkerStatusService
{
    private readonly string _filePath;

    public WorkerStatusService(IConfiguration configuration)
    {
        _filePath =
            configuration["Files:WorkerStatusFile"]
            ?? "Output/worker_status.json";
    }

    public async Task WriteAsync(WorkerStatus status, CancellationToken ct)
    {
        status.UpdatedAtUtc = DateTime.UtcNow;

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(status, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        // Same atomic-write pattern as the processed-state file: write to a
        // temp file then move, so the Web side (reading concurrently from a
        // separate process) never sees a half-written/corrupt JSON file.
        var tmpPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tmpPath, json, ct);
        File.Move(tmpPath, _filePath, overwrite: true);
    }
}
