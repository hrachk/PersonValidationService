namespace PersonValidationService.Models;

public sealed class DicFirstName
{
    public int FirstNameId { get; set; }

    /// <summary>Raw value as decoded by the connector under the column's
    /// declared latin1 charset — the table actually stores UTF-8 encoded
    /// Armenian text byte-for-byte under that charset tag, so this needs
    /// re-decoding (see PersonRepository.FixLatin1Mojibake) before use.</summary>
    public string FirstName { get; set; } = string.Empty;
}
