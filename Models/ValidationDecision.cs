 

namespace PersonValidationService.Models
{
    public sealed class ValidationDecision
    {
        public long PersonId { get; set; }

        /// <summary>
        /// Other PersonIds detected as the same real human (via shared
        /// PassportNum) and merged into this decision. Empty/just
        /// [PersonId] when no linkage was found.
        /// </summary>
        public List<long> LinkedPersonIds { get; set; } = [];

        public string Status { get; set; } = string.Empty;

        public string? SelectedSsn { get; set; }

        public List<string> SsnCandidates { get; set; } = [];

        public string? Reason { get; set; }

        public List<PassportCheckResult> Checks { get; set; } = [];
    }
}
