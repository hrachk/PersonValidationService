using System.Text.Json;
using PersonValidationService.Models;

namespace PersonValidationService.Web.Services;

/// <summary>
/// Reads the small JSON status file the Worker process writes on every poll
/// (Idle/Scanning/Processing) so the dashboard can show a live "what's the
/// background process doing right now" indicator. The two processes never
/// talk to each other directly — this file is the entire contract.
/// </summary>
public sealed class WorkerStatusReaderService
{
    private readonly string _filePath;
    private readonly ILogger<WorkerStatusReaderService> _logger;

    public WorkerStatusReaderService(
        IConfiguration configuration,
        ILogger<WorkerStatusReaderService> logger)
    {
        _filePath = configuration["Files:WorkerStatus"] ?? "../Output/worker_status.json";
        _logger = logger;
    }

    public WorkerStatus? Read()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            return JsonSerializer.Deserialize<WorkerStatus>(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Worker may be mid-write (rare, given the temp-file+move pattern
            // it uses) — just skip this poll, the next one will pick up the
            // settled file. Not worth surfacing as an error to the user.
            _logger.LogDebug(ex, "Transient read failure for worker status at {Path}", _filePath);
            return null;
        }
    }
}
