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
    private readonly IHostApplicationLifetime _lifetime;

    public ValidationWorker(
        ILogger<ValidationWorker> logger,
        IConfiguration configuration,
        FilePersonReader filePersonReader,
        PersonRepository personRepository,
        ValidatorApiClient validatorApiClient,
        DecisionService decisionService,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _configuration = configuration;
        _filePersonReader = filePersonReader;
        _personRepository = personRepository;
        _validatorApiClient = validatorApiClient;
        _decisionService = decisionService;
        _lifetime = lifetime;
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

                var checks =
                    new List<(string Passport, ValidatorResponse? Response, string? Error)>();

                foreach (var passport in dbDocuments)
                {
                    try
                    {
                        var response =
                            await _validatorApiClient.ValidateAsync(
                                passport,
                                stoppingToken);

                        checks.Add((passport, response, null));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "API_ERROR for PersonId={PersonId} Passport={Passport}",
                            personId,
                            passport);

                        checks.Add((passport, null, ex.Message));
                    }
                }

                await _decisionService.ProcessPersonAsync(
                    personId,
                    dbDocuments,
                    checks,
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

        _lifetime.StopApplication();
    }
}
