using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PersonValidationService.Models;

namespace PersonValidationService.Web.Services;

/// <summary>
/// Reads Output/decisions.json produced by JsonReportWriter.
/// That file is a stream of pretty-printed JSON objects written back-to-back
/// (one per WriteAsync call), not a valid JSON array. Top-level object
/// boundaries are always un-indented, so a "}" immediately followed by a
/// newline and an un-indented "{" reliably marks the gap between two
/// records - nested objects inside "Checks" always have either a trailing
/// comma or leading indentation at that position, so they never match.
/// </summary>
public sealed class DecisionReportService
{
    private static readonly Regex RecordBoundary =
        new(@"\}\s*\r?\n\{", RegexOptions.Compiled);

    private readonly string _filePath;
    private readonly ILogger<DecisionReportService> _logger;

    public DecisionReportService(
        IConfiguration configuration,
        ILogger<DecisionReportService> logger)
    {
        _filePath = configuration["Files:DecisionReport"] ?? "Output/decisions.json";
        _logger = logger;
    }

    public List<ValidationDecision> ReadAll()
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("Decision report not found at {Path}", _filePath);
            return [];
        }

        string raw;

        try
        {
            using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            using var reader = new StreamReader(stream, Encoding.UTF8);
            raw = reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read decision report at {Path}", _filePath);
            return [];
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var joined = "[" + RecordBoundary.Replace(raw.TrimEnd(), "},\n{") + "]";

        try
        {
            return JsonSerializer.Deserialize<List<ValidationDecision>>(joined) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse decision report at {Path}", _filePath);
            return [];
        }
    }
}
