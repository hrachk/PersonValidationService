namespace PersonValidationService.Models;

public sealed class ValidationResult
{
    public long PersonId { get; set; }

    public string Passport { get; set; } = string.Empty;

    public bool IsValid { get; set; }

    public string? Ssn { get; set; }

    public string? Error { get; set; }
}