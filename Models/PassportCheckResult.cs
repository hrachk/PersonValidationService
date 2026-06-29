using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersonValidationService.Models
{
    public sealed class PassportCheckResult
    {
        public string Passport { get; set; } = string.Empty;

        public bool DocumentsMatch { get; set; }

        public string? Ssn { get; set; }

        public List<string> DbDocuments { get; set; } = [];

        public List<string> BprDocuments { get; set; } = [];

        public string? BprFirstName { get; set; }

        public string? BprLastName { get; set; }

        public DateTime? BprBirthDate { get; set; }

        public string? Error { get; set; }
    }
}
