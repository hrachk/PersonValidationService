namespace PersonValidationService.Services;

public sealed class FilePersonReader
{
    public async Task<List<long>> ReadPersonIdsAsync(
        string path,
        CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(path, ct);

        return lines
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(long.Parse)
            .Distinct()
            .ToList();
    }
}