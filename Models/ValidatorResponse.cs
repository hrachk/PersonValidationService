using System.Text.Json.Serialization;

namespace PersonValidationService.Models;

public sealed class ValidatorDocument
{
    [JsonPropertyName("documentNumber")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("documentStatus")]
    public string? DocumentStatus { get; set; }
}
public sealed class ValidatorResponse
{
    [JsonPropertyName("ekengStatus")]
    public bool EkengStatus { get; set; }

    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("ssn")]
    public string? Ssn { get; set; }

    [JsonPropertyName("persons")]
    public List<ValidatorPerson>? Persons { get; set; }
}

public sealed class ValidatorPerson
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("pNum")]
    public string? PNum { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("birthDate")]
    public DateTime BirthDate { get; set; }

    [JsonPropertyName("bpR_Documents")]
    public List<ValidatorDocument>? Documents { get; set; }
    [JsonPropertyName("documentNumber")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("documentStatus")]
    public string? DocumentStatus { get; set; }

}