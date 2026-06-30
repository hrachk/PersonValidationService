using PersonValidationService.Models;
using PersonValidationService.Services;

namespace PersonValidationService.Workers;

/// <summary>
/// Continuously watches Files:InputFile (personIds.txt) for newly-added
/// PersonId lines and processes only the ones not seen before. Unlike the
/// old one-shot batch run, this never stops on its own — it keeps polling
/// for as long as the host is running, so appending new PersonIds to the
/// file is enough to get them picked up, no restart needed.
///
/// "Already processed" is tracked via a small state file (one PersonId per
/// line) rather than purely in memory, so a service restart doesn't
/// re-validate everything that was already done (which would mean
/// redundant external Validator API calls and DB writes).
/// </summary>
public sealed class ValidationWorker : BackgroundService
{
    private readonly ILogger<ValidationWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly FilePersonReader _filePersonReader;
    private readonly PersonRepository _personRepository;
    private readonly ValidatorApiClient _validatorApiClient;
    private readonly DecisionService _decisionService;
    private readonly WorkerStatusService _workerStatusService;

    public ValidationWorker(
        ILogger<ValidationWorker> logger,
        IConfiguration configuration,
        FilePersonReader filePersonReader,
        PersonRepository personRepository,
        ValidatorApiClient validatorApiClient,
        DecisionService decisionService,
        WorkerStatusService workerStatusService)
    {
        _logger = logger;
        _configuration = configuration;
        _filePersonReader = filePersonReader;
        _personRepository = personRepository;
        _validatorApiClient = validatorApiClient;
        _decisionService = decisionService;
        _workerStatusService = workerStatusService;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var file = _configuration["Files:InputFile"]!;

        var statePath = _configuration["Files:ProcessedStateFile"]
            ?? Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".",
                "processed_person_ids.txt");

        var pollInterval = TimeSpan.FromSeconds(
            _configuration.GetValue("Files:WatchPollSeconds", 10));

        _logger.LogInformation(
            "START validation watch — file={File} state={State} pollEvery={Poll}s",
            file, statePath, pollInterval.TotalSeconds);

        var processedPersonIds = await LoadProcessedStateAsync(statePath, stoppingToken);

        _logger.LogInformation(
            "Loaded {Count} already-processed PersonId(s) from state file",
            processedPersonIds.Count);

        await _workerStatusService.WriteAsync(new() { State = "Idle" }, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(file, statePath, processedPersonIds, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watch loop iteration failed, will retry next poll");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("STOPPING validation watch (shutdown requested)");
    }

    private async Task PollOnceAsync(
        string file,
        string statePath,
        HashSet<long> processedPersonIds,
        CancellationToken ct)
    {
        await _workerStatusService.WriteAsync(new() { State = "Scanning" }, ct);

        List<long> personIds;
        try
        {
            personIds = await _filePersonReader.ReadPersonIdsAsync(file, ct);
        }
        catch (FileNotFoundException)
        {
            // Input file may not exist yet on a fresh deploy — just wait for it.
            await _workerStatusService.WriteAsync(new() { State = "Idle" }, ct);
            return;
        }

        var newIds = personIds.Where(id => !processedPersonIds.Contains(id)).ToList();
        if (newIds.Count == 0)
        {
            await _workerStatusService.WriteAsync(new() { State = "Idle" }, ct);
            return;
        }

        _logger.LogInformation("Found {Count} new PersonId(s) in {File}", newIds.Count, file);

        var stateDirty = false;
        var batchDone = 0;

        foreach (var personId in newIds)
        {
            ct.ThrowIfCancellationRequested();

            if (processedPersonIds.Contains(personId))
                continue; // absorbed into an earlier group within this same batch

            await _workerStatusService.WriteAsync(new()
            {
                State = "Processing",
                CurrentPersonId = personId,
                BatchTotal = newIds.Count,
                BatchDone = batchDone,
                LastActivityUtc = DateTime.UtcNow
            }, ct);

            List<long> group;
            try
            {
                group = await _personRepository.GetLinkedPersonIdsAsync(personId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to resolve linked-group for PersonId={PersonId}, will retry next poll",
                    personId);
                batchDone++;
                continue; // not marked processed — retried on the next poll
            }

            if (group.Any(processedPersonIds.Contains))
            {
                // Another member of this group was already processed in an
                // earlier poll (e.g. file had both PersonIds, one arrived
                // before its linked sibling) — just mark the rest, no need
                // to redo the whole decision.
                foreach (var id in group)
                    processedPersonIds.Add(id);
                stateDirty = true;
                batchDone++;
                continue;
            }

            if (group.Count > 1)
            {
                _logger.LogInformation(
                    "PersonId={PersonId} linked to {Count} other PersonId(s) via shared passport: {Linked}",
                    personId,
                    group.Count - 1,
                    string.Join(",", group.Where(x => x != personId)));
            }

            try
            {
                var dbDocuments = await _personRepository.GetPassportsForGroupAsync(group, ct);

                var checks = new List<(string Passport, ValidatorResponse? Response, string? Error)>();

                foreach (var passport in dbDocuments)
                {
                    try
                    {
                        var response = await _validatorApiClient.ValidateAsync(passport, ct);
                        checks.Add((passport, response, null));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "API_ERROR for PersonId={PersonId} Passport={Passport}",
                            personId,
                            passport);

                        checks.Add((passport, null, ex.Message));
                    }
                }

                await _decisionService.ProcessPersonAsync(
                    personId,
                    group,
                    dbDocuments,
                    checks,
                    ct);

                // Only mark as processed once the decision was fully
                // written — an infra failure above (DB unreachable, etc.)
                // leaves these unmarked so they're retried next poll.
                foreach (var id in group)
                    processedPersonIds.Add(id);
                stateDirty = true;

                _logger.LogInformation("Processed PersonId={PersonId}", personId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Person processing failed {PersonId}, will retry next poll",
                    personId);
            }

            batchDone++;
        }

        if (stateDirty)
            await PersistProcessedStateAsync(statePath, processedPersonIds, ct);

        await _workerStatusService.WriteAsync(new() { State = "Idle" }, ct);
    }

    private static async Task<HashSet<long>> LoadProcessedStateAsync(
        string path,
        CancellationToken ct)
    {
        var ids = new HashSet<long>();

        if (!File.Exists(path))
            return ids;

        var lines = await File.ReadAllLinesAsync(path, ct);
        foreach (var line in lines)
        {
            if (long.TryParse(line.Trim(), out var id))
                ids.Add(id);
        }

        return ids;
    }

    private static async Task PersistProcessedStateAsync(
        string path,
        HashSet<long> ids,
        CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Write to a temp file then move, so a crash mid-write never leaves
        // a truncated/corrupt state file behind.
        var tmpPath = path + ".tmp";
        await File.WriteAllLinesAsync(
            tmpPath,
            ids.OrderBy(x => x).Select(x => x.ToString()),
            ct);

        File.Move(tmpPath, path, overwrite: true);
    }
}
