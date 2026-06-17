using System.Net.Http.Json;
using PersonValidationService.Models;

namespace PersonValidationService.Services;

public sealed class ValidatorApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ValidatorApiClient> _logger;

    public ValidatorApiClient(
        HttpClient httpClient,
        ILogger<ValidatorApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ValidatorResponse?> ValidateAsync(
        string passport,
        CancellationToken ct)
    {
        var request = new ValidatorRequest
        {
            Passport = passport
        };

        var response = await _httpClient.PostAsJsonAsync(
            "Validator/ValidateNoFiltered",
            request,
            ct);


        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<ValidatorResponse>(cancellationToken: ct);
    }
}