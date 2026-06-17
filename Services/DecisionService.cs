using PersonValidationService.Models;

namespace PersonValidationService.Services;

public sealed class DecisionService
{
    private readonly PersonRepository _personRepository;
    private readonly DocumentComparisonService _documentComparisonService;
    private readonly JsonReportWriter _jsonReportWriter;
    private readonly ILogger<DecisionService> _logger;

    public DecisionService(
        PersonRepository personRepository,
        DocumentComparisonService documentComparisonService,
        JsonReportWriter jsonReportWriter,
        ILogger<DecisionService> logger)
    {
        _personRepository = personRepository;
        _documentComparisonService = documentComparisonService;
        _jsonReportWriter = jsonReportWriter;
        _logger = logger;
    }

    public async Task ProcessPersonAsync(
        long personId,
        List<string> dbDocuments,
        List<(string Passport, ValidatorResponse? Response, string? Error)> checks,
        CancellationToken ct)
    {
        var decision = new ValidationDecision
        {
            PersonId = personId
        };

        if (dbDocuments.Count == 0)
        {
            decision.Status = "NO_DOCUMENTS_IN_DB";
            decision.Reason = "Person has no passport records in the database.";

            await _jsonReportWriter.WriteAsync(decision);
            return;
        }

        foreach (var (passport, response, error) in checks)
        {
            var check = new PassportCheckResult
            {
                Passport = passport,
                DbDocuments = dbDocuments
            };

            if (error != null)
            {
                check.Error = error;
                decision.Checks.Add(check);
                continue;
            }

            if (response == null)
            {
                check.Error = "EMPTY_RESPONSE";
                decision.Checks.Add(check);
                continue;
            }

            var bprDocuments = (response.Persons ?? [])
                .SelectMany(p => (p.Documents ?? []).Select(d => d.DocumentNumber))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();

            check.BprDocuments = bprDocuments;
            check.DocumentsMatch = _documentComparisonService.IsMatch(dbDocuments, bprDocuments);
            check.Ssn = response.Ssn;

            if (!string.IsNullOrWhiteSpace(response.Ssn) &&
                !decision.SsnCandidates.Contains(response.Ssn))
            {
                decision.SsnCandidates.Add(response.Ssn);
            }

            decision.Checks.Add(check);
        }

        DecideStatus(decision, checks);

        _logger.LogInformation(
            "Decision for PersonId={PersonId}: Status={Status} Ssn={Ssn}",
            personId,
            decision.Status,
            decision.SelectedSsn);

        await _jsonReportWriter.WriteAsync(decision);

        if (decision.Status == "VALID" && decision.SelectedSsn != null)
        {
            await _personRepository.UpdateSocialCardAsync(
                personId,
                decision.SelectedSsn,
                ct);
        }
    }

    private static void DecideStatus(
        ValidationDecision decision,
        List<(string Passport, ValidatorResponse? Response, string? Error)> checks)
    {
        var successfulChecks = decision.Checks
            .Where(c => c.Error == null)
            .ToList();

        if (successfulChecks.Count == 0)
        {
            decision.Status = "ERROR";
            decision.Reason = "All passport checks failed (API errors).";
            return;
        }

        if (decision.SsnCandidates.Count == 0)
        {
            decision.Status = "NO_SSN";
            decision.Reason = "Validator did not return an SSN for any passport.";
            return;
        }

        if (decision.SsnCandidates.Count > 1)
        {
            decision.Status = "AMBIGUOUS";
            decision.Reason =
                $"Multiple distinct SSN candidates found: {string.Join(", ", decision.SsnCandidates)}.";
            return;
        }

        var ssn = decision.SsnCandidates[0];

        var hasDocumentMismatch = successfulChecks
            .Where(c => c.Ssn == ssn)
            .Any(c => !c.DocumentsMatch);

        var hasInvalidResponse = checks
            .Where(c => c.Response != null && c.Response.Ssn == ssn)
            .Any(c => !c.Response!.IsValid);

        if (hasInvalidResponse)
        {
            decision.Status = "INVALID";
            decision.Reason = "Validator marked the document as invalid.";
            return;
        }

        if (hasDocumentMismatch)
        {
            decision.Status = "MISMATCH";
            decision.Reason = "Documents on file do not match BPR records.";
            return;
        }

        decision.Status = "VALID";
        decision.SelectedSsn = ssn;
    }
}
