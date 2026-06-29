namespace PersonValidationService.Web.Services;

public static class StatusPresentation
{
    public static string ToCssClass(string status) => status switch
    {
        "VALID" => "status-valid",
        "MISMATCH" => "status-mismatch",
        "AMBIGUOUS" => "status-ambiguous",
        "NO_SSN" => "status-no-ssn",
        "INVALID" => "status-invalid",
        "ERROR" => "status-error",
        "NO_DOCUMENTS_IN_DB" => "status-no-docs",
        _ => "status-unknown"
    };

    public static string ToIcon(string status) => status switch
    {
        "VALID" => "✓",
        "MISMATCH" => "✗",
        "AMBIGUOUS" => "❓",
        "NO_SSN" => "∅",
        "INVALID" => "⊘",
        "ERROR" => "⚠",
        "NO_DOCUMENTS_IN_DB" => "📄",
        _ => "•"
    };

    public static string FieldLabel(string field) => field switch
    {
        "FirstName" => "Имя",
        "LastName" => "Фамилия",
        "BirthDate" => "Дата рождения",
        _ => field
    };
}
