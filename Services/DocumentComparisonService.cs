namespace PersonValidationService.Services;

public sealed class DocumentComparisonService
{
    public bool IsMatch(
        IEnumerable<string> dbDocuments,
        IEnumerable<string> bprDocuments)
    {
        var dbSet = dbDocuments
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize)
            .ToHashSet();

        var bprSet = bprDocuments
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize)
            .ToHashSet();

        return dbSet.SetEquals(bprSet);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}