namespace KuechenRezepte.Services;

public sealed class ApiSecurityOptions
{
    public string? MealPlanApiKey { get; set; }
}

public sealed class AlexaSecurityOptions
{
    public string? SkillId { get; set; }
    public bool ValidateSignature { get; set; } = true;
    public bool ValidateTimestamp { get; set; } = true;
    public int MaxRequestAgeSeconds { get; set; } = 150;
    public List<string> AllowedClientIps { get; set; } = new();
}
