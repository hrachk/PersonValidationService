using PersonValidationService.Models;

namespace PersonValidationService.Services;

public sealed class DecisionService
{
    private readonly PersonRepository _personRepository;
    private readonly DocumentComparisonService _documentComparisonService;

    public DecisionService(
        PersonRepository personRepository,
        DocumentComparisonService documentComparisonService)
    {
        _personRepository = personRepository;
        _documentComparisonService = documentComparisonService;
    }

    public async Task ProcessPersonAsync(
        long personId,
        List<string> dbDocuments,
        List<ValidatorResponse> responses,
        CancellationToken ct)
    {

    }
}