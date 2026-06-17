using PersonValidationService.Models;

namespace PersonValidationService.Services;

public sealed class CsvResultWriter
{
    private readonly string _filePath;

    public CsvResultWriter(IConfiguration configuration)
    {
        _filePath = configuration["Files:OutputFile"]!;
    }

    public async Task WriteAsync(ValidationResult result)
    {
        if (!File.Exists(_filePath))
        {
            await File.WriteAllTextAsync(
                _filePath,
                "PersonId,Passport,IsValid,Ssn,Error" + Environment.NewLine);
        }

        var line = $"{result.PersonId},{result.Passport},{result.IsValid},{result.Ssn},{result.Error}";

        await File.AppendAllTextAsync(_filePath, line + Environment.NewLine);
    }
}
