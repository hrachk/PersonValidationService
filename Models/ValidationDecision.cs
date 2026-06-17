 

namespace PersonValidationService.Models
{
    public sealed class ValidationDecision
    {
        public long PersonId { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? SelectedSsn { get; set; }

        public List<string> SsnCandidates { get; set; } = [];

        public string? Reason { get; set; }

        public List<PassportCheckResult> Checks { get; set; } = [];
    }
}
