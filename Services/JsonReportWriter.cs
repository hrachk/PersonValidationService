using System.Text.Json;
using PersonValidationService.Models;

namespace PersonValidationService.Services;

public sealed class JsonReportWriter
{
    private readonly string _filePath;

    public JsonReportWriter(IConfiguration configuration)
    {
        _filePath =
            configuration["Files:DecisionReport"]!;
    }

    public async Task WriteAsync(
        ValidationDecision decision)
    {
        var json =
            JsonSerializer.Serialize(
                decision,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        await File.AppendAllTextAsync(
            _filePath,
            json + Environment.NewLine);
    }
}