 

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

        /// <summary>Our own FirstName/LastName/BirthDate on file, used for the
        /// comparison against BPR's (internally-consistent) identity.</summary>
        public string? DbFirstName { get; set; }
        public string? DbLastName { get; set; }
        public DateTime? DbBirthDate { get; set; }

        /// <summary>Which field(s) — FirstName/LastName/BirthDate — disagreed
        /// when comparing BPR's identity against our Persons record. Empty
        /// when there was no mismatch (or no comparison was reached).</summary>
        public List<string> MismatchedFields { get; set; } = [];

        public List<PassportCheckResult> Checks { get; set; } = [];
    }
}
