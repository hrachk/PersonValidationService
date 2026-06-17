namespace PersonValidationService.Services;

public sealed class FilePersonReader
{
    private readonly ILogger<FilePersonReader> _logger;

    public FilePersonReader(ILogger<FilePersonReader> logger)
    {
        _logger = logger;
    }

    public async Task<List<long>> ReadPersonIdsAsync(
        string path,
        CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(path, ct);

        var ids = new List<long>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (long.TryParse(trimmed, out var id))
            {
                ids.Add(id);
            }
            else
            {
                _logger.LogWarning(
                    "Skipping invalid PersonId line: '{Line}'",
                    line);
            }
        }

        return ids.Distinct().ToList();
    }
}
