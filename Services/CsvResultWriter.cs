using System.Text;
using PersonValidationService.Models;

namespace PersonValidationService.Services;

public sealed class CsvResultWriter
{
    private readonly string _filePath;
    private bool _headerWritten = false;

    public CsvResultWriter(IConfiguration configuration)
    {
        _filePath = configuration["Files:OutputFile"]!;
    }

    public async Task WriteAsync(ValidationResult result)
    {
        var line = $"{result.PersonId},{result.Passport},{result.IsValid},{result.Ssn},{result.Error}";

        if (!_headerWritten)
        {
            var header = "PersonId,Passport,IsValid,Ssn,Error";
            await File.AppendAllTextAsync(_filePath, header + Environment.NewLine);
            _headerWritten = true;
        }

        await File.AppendAllTextAsync(_filePath, line + Environment.NewLine);
    }
}