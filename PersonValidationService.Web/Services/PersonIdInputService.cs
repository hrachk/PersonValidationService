namespace PersonValidationService.Web.Services;

public sealed record AddPersonIdsResult(
    List<long> Added,
    List<long> AlreadyQueued,
    List<long> AlreadyProcessed,
    List<string> Invalid);

/// <summary>
/// Lets the dashboard add new PersonIds to Input/personIds.txt directly —
/// the same file ValidationWorker already polls every few seconds — instead
/// of requiring someone to edit it by hand. Web only ever appends; Worker
/// only ever reads, so there's no concurrent-write hazard between the two
/// processes, just a single writer guarded here against concurrent requests
/// from the Web side itself (e.g. two browser tabs submitting at once).
/// </summary>
public sealed class PersonIdInputService
{
    private readonly string _inputFilePath;
    private readonly string _processedStatePath;
    private readonly ILogger<PersonIdInputService> _logger;
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public PersonIdInputService(
        IConfiguration configuration,
        ILogger<PersonIdInputService> logger)
    {
        _inputFilePath = configuration["Files:InputFile"] ?? "../Input/personIds.txt";
        _processedStatePath = configuration["Files:ProcessedStateFile"] ?? "../Output/processed_person_ids.txt";
        _logger = logger;
    }

    /// <summary>
    /// Parses free-form text (one or many PersonIds, separated by any mix
    /// of newlines/commas/spaces/tabs/semicolons — a "smart paste"), checks
    /// each against what's already queued or already processed, and appends
    /// only the genuinely new ones to the input file.
    /// </summary>
    public async Task<AddPersonIdsResult> AddAsync(string rawInput, CancellationToken ct)
    {
        var tokens = rawInput.Split(
            [',', ';', ' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var invalid = new List<string>();
        var requested = new List<long>();

        foreach (var token in tokens)
        {
            if (long.TryParse(token, out var id) && id > 0)
                requested.Add(id);
            else
                invalid.Add(token);
        }

        requested = requested.Distinct().ToList();

        await WriteLock.WaitAsync(ct);
        try
        {
            var alreadyQueued = await ReadExistingIdsAsync(_inputFilePath, ct);
            var alreadyProcessed = await ReadExistingIdsAsync(_processedStatePath, ct);

            var toAdd = requested
                .Where(id => !alreadyQueued.Contains(id) && !alreadyProcessed.Contains(id))
                .ToList();

            if (toAdd.Count > 0)
            {
                var dir = Path.GetDirectoryName(_inputFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                // Plain append — Worker only reads this file (FileShare.Read
                // on its side via File.ReadAllLinesAsync), so a normal
                // append here is safe; the WriteLock above only protects
                // against two concurrent Web requests racing each other.
                await File.AppendAllLinesAsync(
                    _inputFilePath,
                    toAdd.Select(x => x.ToString()),
                    ct);

                _logger.LogInformation(
                    "Added {Count} new PersonId(s) via Web UI: {Ids}",
                    toAdd.Count,
                    string.Join(",", toAdd));
            }

            return new AddPersonIdsResult(
                Added: toAdd,
                AlreadyQueued: requested.Where(id => alreadyQueued.Contains(id)).ToList(),
                AlreadyProcessed: requested.Where(id => alreadyProcessed.Contains(id) && !alreadyQueued.Contains(id)).ToList(),
                Invalid: invalid);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private static async Task<HashSet<long>> ReadExistingIdsAsync(string path, CancellationToken ct)
    {
        var ids = new HashSet<long>();

        if (!File.Exists(path))
            return ids;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (long.TryParse(line.Trim(), out var id))
                    ids.Add(id);
            }
        }
        catch (IOException)
        {
            // Worker is mid-write on its state file (rare) — treat as
            // "unknown for this attempt" rather than failing the add; worst
            // case a since-processed id gets re-queued and the worker just
            // no-ops it on the next poll.
        }

        return ids;
    }
}
