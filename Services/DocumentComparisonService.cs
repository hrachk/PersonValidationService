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

        // A match means at least one document we have on file also appears
        // in BPR's response — not that the two sets are identical. BPR can
        // legitimately return more (or fewer) documents than we have for a
        // given identity; requiring exact equality flagged real matches as
        // MISMATCH whenever the counts differed.
        return dbSet.Overlaps(bprSet);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}