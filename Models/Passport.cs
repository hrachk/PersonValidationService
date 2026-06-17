namespace PersonValidationService.Models;

public sealed class Passport
{
    public int PassportId { get; set; }

    public int? PersonId { get; set; }

    public string? PassportNum { get; set; }
}