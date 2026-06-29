namespace PersonValidationService.Models;

public sealed class DicLastName
{
    public int LastNameId { get; set; }

    /// <summary>Same latin1/UTF-8 mojibake caveat as DicFirstName.FirstName —
    /// see PersonRepository.FixLatin1Mojibake.</summary>
    public string LastName { get; set; } = string.Empty;
}
