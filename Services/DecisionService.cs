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
        List<long> linkedPersonIds,
        List<string> dbDocuments,
        List<(string Passport, ValidatorResponse? Response, string? Error)> checks,
        CancellationToken ct)
    {
        var decision = new ValidationDecision
        {
            PersonId = personId,
            LinkedPersonIds = linkedPersonIds
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

            // Kept for diagnostics/visibility in the report only — per the
            // spec, document-number matching is NOT part of the VALID
            // decision criteria (see DecideStatusAsync), only name/surname/
            // birthdate/SSN are.
            check.BprDocuments = bprDocuments;
            check.DocumentsMatch = _documentComparisonService.IsMatch(dbDocuments, bprDocuments);
            check.Ssn = response.Ssn;

            var bprPerson = response.Persons?.FirstOrDefault();
            check.BprFirstName = bprPerson?.FirstName;
            check.BprLastName = bprPerson?.LastName;
            check.BprBirthDate = bprPerson?.BirthDate;

            if (!string.IsNullOrWhiteSpace(response.Ssn) &&
                !decision.SsnCandidates.Contains(response.Ssn))
            {
                decision.SsnCandidates.Add(response.Ssn);
            }

            decision.Checks.Add(check);
        }

        var dbIdentity = await _personRepository.GetPersonIdentityAsync(personId, ct);
        decision.DbFirstName = dbIdentity?.FirstName;
        decision.DbLastName = dbIdentity?.LastName;
        decision.DbBirthDate = dbIdentity?.BirthDate;

        DecideStatus(decision, checks);

        _logger.LogInformation(
            "Decision for PersonId={PersonId}: Status={Status} Ssn={Ssn}",
            personId,
            decision.Status,
            decision.SelectedSsn);

        await _jsonReportWriter.WriteAsync(decision);

        if (decision.Status == "VALID" && decision.SelectedSsn != null)
        {
            await _personRepository.UpdateSocialCardForGroupAsync(
                linkedPersonIds.Count > 0 ? linkedPersonIds : [personId],
                decision.SelectedSsn,
                ct);
        }
    }

    private static string? NormalizeName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

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

        var checksForSsn = successfulChecks.Where(c => c.Ssn == ssn).ToList();

        // BPR-internal consistency: every passport query for this person
        // must agree on FirstName/LastName/BirthDate, not just SSN.
        var identityTuples = checksForSsn
            .Select(c => (
                First: NormalizeName(c.BprFirstName),
                Last: NormalizeName(c.BprLastName),
                Dob: c.BprBirthDate?.Date))
            .Where(t => t.First != null || t.Last != null || t.Dob != null)
            .Distinct()
            .ToList();

        if (identityTuples.Count > 1)
        {
            decision.Status = "AMBIGUOUS";
            decision.Reason =
                "BPR responses for this person's passports disagree with each other on name/surname/birthdate.";
            return;
        }

        var hasInvalidResponse = checks
            .Where(c => c.Response != null && c.Response.Ssn == ssn)
            .Any(c => !c.Response!.IsValid);

        if (hasInvalidResponse)
        {
            decision.Status = "INVALID";
            decision.Reason = "Validator marked the document as invalid.";
            return;
        }

        // No BPR identity at all to compare against our Persons record —
        // can't confirm a name/DOB match, so don't write the SocialCard.
        if (identityTuples.Count == 0)
        {
            decision.Status = "NO_SSN";
            decision.Reason = "Validator returned an SSN but no name/surname/birthdate to confirm identity against.";
            return;
        }

        var bprIdentity = identityTuples[0];
        var dbFirst = NormalizeName(decision.DbFirstName);
        var dbLast = NormalizeName(decision.DbLastName);
        var dbDob = decision.DbBirthDate?.Date;

        var mismatched = new List<string>();
        if (bprIdentity.First != dbFirst) mismatched.Add("FirstName");
        if (bprIdentity.Last != dbLast) mismatched.Add("LastName");
        if (bprIdentity.Dob != dbDob) mismatched.Add("BirthDate");

        if (mismatched.Count > 0)
        {
            decision.Status = "MISMATCH";
            decision.MismatchedFields = mismatched;
            decision.Reason =
                $"BPR identity confirmed (valid), but does not match our Persons record on: {string.Join(", ", mismatched)}.";
            return;
        }

        decision.Status = "VALID";
        decision.SelectedSsn = ssn;
    }
}
