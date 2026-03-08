using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace KuechenRezepte.Services;

public class AlexaSkillService
{
    private readonly IMealPlanDayService _mealPlanDayService;
    private readonly AlexaSecurityOptions _securityOptions;

    public AlexaSkillService(IMealPlanDayService mealPlanDayService, IOptions<AlexaSecurityOptions>? securityOptions = null)
    {
        _mealPlanDayService = mealPlanDayService;
        _securityOptions = securityOptions?.Value ?? new AlexaSecurityOptions();
    }

    public bool TryValidateRequest(AlexaRequestEnvelope requestEnvelope, out string error)
    {
        error = string.Empty;
        if (requestEnvelope.Request == null)
        {
            error = "Missing Alexa request payload.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_securityOptions.SkillId))
        {
            var appId = requestEnvelope.Session?.Application?.ApplicationId;
            if (!string.Equals(appId, _securityOptions.SkillId, StringComparison.Ordinal))
            {
                error = "Invalid Alexa skill id.";
                return false;
            }
        }

        if (_securityOptions.ValidateTimestamp)
        {
            if (string.IsNullOrWhiteSpace(requestEnvelope.Request.Timestamp) ||
                !DateTimeOffset.TryParse(requestEnvelope.Request.Timestamp, out var requestTimestamp))
            {
                error = "Missing or invalid Alexa timestamp.";
                return false;
            }

            var maxAge = Math.Max(30, _securityOptions.MaxRequestAgeSeconds);
            var age = Math.Abs((DateTimeOffset.UtcNow - requestTimestamp.ToUniversalTime()).TotalSeconds);
            if (age > maxAge)
            {
                error = "Alexa timestamp outside accepted window.";
                return false;
            }
        }

        return true;
    }

    public async Task<AlexaResponseEnvelope> HandleAsync(
        AlexaRequestEnvelope requestEnvelope,
        CancellationToken cancellationToken = default)
    {
        var request = requestEnvelope.Request;
        if (request == null || string.IsNullOrWhiteSpace(request.Type))
        {
            return BuildResponse("Ungültige Anfrage an den Alexa Skill.");
        }

        if (string.Equals(request.Type, "LaunchRequest", StringComparison.OrdinalIgnoreCase))
        {
            return BuildResponse("Willkommen bei KüchenRezepte. Du kannst fragen: Was gibt es heute, was gibt es morgen oder was gibt es am Datum.");
        }

        if (!string.Equals(request.Type, "IntentRequest", StringComparison.OrdinalIgnoreCase))
        {
            return BuildResponse("Diese Anfrage wird aktuell nicht unterstützt.");
        }

        var intentName = request.Intent?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(intentName))
        {
            return BuildResponse("Ich habe den Intent nicht erkannt.");
        }

        if (IsIntent(intentName, "AMAZON.HelpIntent", "HelpIntent"))
        {
            return BuildResponse("Du kannst sagen: Was gibt es heute, was gibt es morgen oder was gibt es am 10. März 2026.");
        }

        if (IsIntent(intentName, "TodayIntent", "MealPlanTodayIntent"))
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var result = await _mealPlanDayService.GetDayAsync(today, cancellationToken);
            return BuildResponse(result.SpeechText);
        }

        if (IsIntent(intentName, "TomorrowIntent", "MealPlanTomorrowIntent"))
        {
            var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
            var result = await _mealPlanDayService.GetDayAsync(tomorrow, cancellationToken);
            return BuildResponse(result.SpeechText);
        }

        if (IsIntent(intentName, "DayIntent", "MealPlanByDateIntent"))
        {
            var slotValue = ReadSlotValue(request.Intent, "date", "datum");
            if (!TryParseAlexaDate(slotValue, out var slotDate))
            {
                return BuildResponse("Bitte nenne ein konkretes Datum, zum Beispiel 10. März 2026.");
            }

            var result = await _mealPlanDayService.GetDayAsync(slotDate, cancellationToken);
            return BuildResponse(result.SpeechText);
        }

        if (IsIntent(intentName, "AMAZON.FallbackIntent"))
        {
            return BuildResponse("Das habe ich nicht verstanden. Frage zum Beispiel: Was gibt es heute?");
        }

        return BuildResponse("Dieser Intent wird aktuell nicht unterstützt.");
    }

    internal static bool TryParseAlexaDate(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // We currently support explicit dates only (yyyy-MM-dd).
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", out date);
    }

    private static bool IsIntent(string intentName, params string[] candidates)
    {
        return candidates.Any(candidate => string.Equals(intentName, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ReadSlotValue(AlexaIntent? intent, params string[] slotNames)
    {
        var slots = intent?.Slots;
        if (slots == null || slots.Count == 0)
        {
            return null;
        }

        foreach (var slotName in slotNames)
        {
            foreach (var entry in slots)
            {
                if (string.Equals(entry.Key, slotName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value?.Value;
                }
            }
        }

        return null;
    }

    private static AlexaResponseEnvelope BuildResponse(string speechText)
    {
        return new AlexaResponseEnvelope
        {
            Version = "1.0",
            Response = new AlexaResponse
            {
                ShouldEndSession = true,
                OutputSpeech = new AlexaOutputSpeech
                {
                    Type = "PlainText",
                    Text = speechText
                }
            }
        };
    }
}

public sealed class AlexaRequestEnvelope
{
    [JsonPropertyName("session")]
    public AlexaSession? Session { get; set; }

    [JsonPropertyName("request")]
    public AlexaRequest? Request { get; set; }
}

public sealed class AlexaRequest
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("intent")]
    public AlexaIntent? Intent { get; set; }
}

public sealed class AlexaSession
{
    [JsonPropertyName("application")]
    public AlexaApplication? Application { get; set; }
}

public sealed class AlexaApplication
{
    [JsonPropertyName("applicationId")]
    public string? ApplicationId { get; set; }
}

public sealed class AlexaIntent
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slots")]
    public Dictionary<string, AlexaSlot>? Slots { get; set; }
}

public sealed class AlexaSlot
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public sealed class AlexaResponseEnvelope
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("response")]
    public AlexaResponse Response { get; set; } = new();
}

public sealed class AlexaResponse
{
    [JsonPropertyName("outputSpeech")]
    public AlexaOutputSpeech OutputSpeech { get; set; } = new();

    [JsonPropertyName("shouldEndSession")]
    public bool ShouldEndSession { get; set; } = true;
}

public sealed class AlexaOutputSpeech
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "PlainText";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
