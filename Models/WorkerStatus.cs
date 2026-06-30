namespace PersonValidationService.Models;

/// <summary>
/// Live processing status, written periodically by ValidationWorker to a
/// small JSON file so the Web dashboard can show what the background
/// process is doing right now, without any direct IPC between the two
/// independent processes.
/// </summary>
public sealed class WorkerStatus
{
    /// <summary>Idle | Scanning | Processing</summary>
    public string State { get; set; } = "Idle";

    /// <summary>PersonId currently being validated (only set while State == Processing).</summary>
    public long? CurrentPersonId { get; set; }

    /// <summary>How many PersonIds were found new in the current/last batch.</summary>
    public int BatchTotal { get; set; }

    /// <summary>How many of BatchTotal have been processed so far in the current/last batch.</summary>
    public int BatchDone { get; set; }

    /// <summary>UTC timestamp of this status snapshot — used by the Web side
    /// to detect a stalled/dead Worker process (status file stops updating).</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last time a new PersonId was actually
    /// picked up and processed (not just an empty poll).</summary>
    public DateTime? LastActivityUtc { get; set; }
}
