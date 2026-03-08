namespace KuechenRezepte.Services;

public sealed class ApiSecurityOptions
{
    public string? MealPlanApiKey { get; set; }
}

public sealed class AlexaSecurityOptions
{
    public string? SkillId { get; set; }
    public bool ValidateTimestamp { get; set; } = true;
    public int MaxRequestAgeSeconds { get; set; } = 150;
}
