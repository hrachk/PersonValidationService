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
        //
        // File.Move on Windows needs delete permission on the destination
        // (rename = delete+create), so a reader without FileShare.Delete
        // open at the wrong instant can make this throw
        // UnauthorizedAccessException. The real fix is on the reader side
        // (WorkerStatusReaderService now opens with FileShare.Delete), but
        // this file is rewritten on every single poll, so a short retry
        // here is cheap insurance against any other transient lock holder
        // (e.g. antivirus scan, Explorer preview pane) — worth it given a
        // swallowed failure just means one missed status update, not lost
        // data, so it's safe to not propagate after retries are exhausted.
        var tmpPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tmpPath, json, ct);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                File.Move(tmpPath, _filePath, overwrite: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 3)
            {
                await Task.Delay(50 * attempt, ct);
            }
        }
    }
}
