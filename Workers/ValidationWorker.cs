using PersonValidationService.Models;
using PersonValidationService.Services;

namespace PersonValidationService.Workers;

public sealed class ValidationWorker : BackgroundService
{
    private readonly ILogger<ValidationWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly FilePersonReader _filePersonReader;
    private readonly PersonRepository _personRepository;
    private readonly ValidatorApiClient _validatorApiClient;
    private readonly DecisionService _decisionService;

    public ValidationWorker(
        ILogger<ValidationWorker> logger,
        IConfiguration configuration,
        FilePersonReader filePersonReader,
        PersonRepository personRepository,
        ValidatorApiClient validatorApiClient,
        DecisionService decisionService)
    {
        _logger = logger;
        _configuration = configuration;
        _filePersonReader = filePersonReader;
        _personRepository = personRepository;
        _validatorApiClient = validatorApiClient;
        _decisionService = decisionService;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("START validation");

        var file =
            _configuration["Files:InputFile"]!;

        var personIds =
            await _filePersonReader.ReadPersonIdsAsync(
                file,
                stoppingToken);

        foreach (var personId in personIds)
        {
            try
            {
                var dbDocuments =
                    await _personRepository.GetPassportsAsync(
                        personId,
                        stoppingToken);

                if (dbDocuments.Count == 0)
                {
                    //await _decisionService.WriteIssueAsync(
                    //    personId,
                    //    "NO_DOCUMENTS_IN_DB",
                    //    null);
                    Console.WriteLine($"No documents found for PersonId={personId}");
                    continue;
                }

                var responses =
                    new List<ValidatorResponse>();

                foreach (var passport in dbDocuments)
                {
                    try
                    {
                        var response =
                            await _validatorApiClient.ValidateAsync(
                                passport,
                                stoppingToken);

                        if (response != null)
                        {
                            responses.Add(response);
                        }
                    }
                    catch (Exception ex)
                    {
                        //await _decisionService.WriteIssueAsync(
                        //    personId,
                        //    "API_ERROR",
                        //    ex.Message);
                        Console.WriteLine($"API_ERROR : Error occurred while validating document for PersonId={personId}: {ex.Message}");
                    }
                }

                await _decisionService.ProcessPersonAsync(
                    personId,
                    dbDocuments,
                    responses,
                    stoppingToken);

                _logger.LogInformation(
                    "Processed PersonId={PersonId}",
                    personId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Person processing failed {PersonId}",
                    personId);
            }
        }

        _logger.LogInformation("FINISH validation");
    }
}