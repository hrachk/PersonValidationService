namespace PersonValidationService.Models;

public sealed class Person
{
    public int PersonId { get; set; }

    public string? SocialCard { get; set; }

    /// <summary>FK into DicFirstNames.FirstNameID — not the name text itself.</summary>
    public int? FirstNameId { get; set; }

    /// <summary>FK into DicLastNames.LastNameID — not the name text itself.</summary>
    public int? LastNameId { get; set; }

    public DateTime? BirthDate { get; set; }
}