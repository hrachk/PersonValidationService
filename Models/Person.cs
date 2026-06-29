namespace PersonValidationService.Models;

public sealed class Person
{
    public int PersonId { get; set; }

    public string? SocialCard { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateTime? BirthDate { get; set; }
}