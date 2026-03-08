using KuechenRezepte.Models;
using KuechenRezepte.Services;
using Microsoft.Extensions.Options;

namespace KuechenRezepte.Tests;

public class AlexaSkillServiceTests
{
    [Fact]
    public async Task HandleAsync_LaunchRequest_ReturnsWelcomeText()
    {
        var sut = CreateService();
        var request = new AlexaRequestEnvelope
        {
            Session = new AlexaSession { Application = new AlexaApplication { ApplicationId = "amzn1.ask.skill.test" } },
            Request = new AlexaRequest { Type = "LaunchRequest", Timestamp = DateTimeOffset.UtcNow.ToString("O") }
        };

        var response = await sut.HandleAsync(request);

        Assert.Contains("Willkommen", response.Response.OutputSpeech.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_TodayIntent_UsesMealPlanServiceSpeech()
    {
        var fake = new FakeMealPlanDayService();
        var sut = CreateService(fake);
        var request = new AlexaRequestEnvelope
        {
            Session = new AlexaSession { Application = new AlexaApplication { ApplicationId = "amzn1.ask.skill.test" } },
            Request = new AlexaRequest
            {
                Type = "IntentRequest",
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                Intent = new AlexaIntent { Name = "TodayIntent" }
            }
        };

        var response = await sut.HandleAsync(request);

        Assert.Contains("FAKE", response.Response.OutputSpeech.Text, StringComparison.Ordinal);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), fake.LastRequestedDate);
    }

    [Fact]
    public async Task HandleAsync_TomorrowIntent_RequestsTomorrowDate()
    {
        var fake = new FakeMealPlanDayService();
        var sut = CreateService(fake);
        var request = new AlexaRequestEnvelope
        {
            Session = new AlexaSession { Application = new AlexaApplication { ApplicationId = "amzn1.ask.skill.test" } },
            Request = new AlexaRequest
            {
                Type = "IntentRequest",
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                Intent = new AlexaIntent { Name = "TomorrowIntent" }
            }
        };

        _ = await sut.HandleAsync(request);

        Assert.Equal(DateOnly.FromDateTime(DateTime.Today).AddDays(1), fake.LastRequestedDate);
    }

    [Fact]
    public async Task HandleAsync_DayIntent_WithDateSlot_UsesProvidedDate()
    {
        var fake = new FakeMealPlanDayService();
        var sut = CreateService(fake);
        var request = new AlexaRequestEnvelope
        {
            Session = new AlexaSession { Application = new AlexaApplication { ApplicationId = "amzn1.ask.skill.test" } },
            Request = new AlexaRequest
            {
                Type = "IntentRequest",
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                Intent = new AlexaIntent
                {
                    Name = "DayIntent",
                    Slots = new Dictionary<string, AlexaSlot>
                    {
                        ["date"] = new AlexaSlot { Value = "2026-03-10" }
                    }
                }
            }
        };

        _ = await sut.HandleAsync(request);

        Assert.Equal(new DateOnly(2026, 3, 10), fake.LastRequestedDate);
    }

    [Fact]
    public async Task HandleAsync_DayIntent_WithInvalidDate_ReturnsGuidance()
    {
        var sut = CreateService();
        var request = new AlexaRequestEnvelope
        {
            Session = new AlexaSession { Application = new AlexaApplication { ApplicationId = "amzn1.ask.skill.test" } },
            Request = new AlexaRequest
            {
                Type = "IntentRequest",
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                Intent = new AlexaIntent
                {
                    Name = "DayIntent",
                    Slots = new Dictionary<string, AlexaSlot>
                    {
                        ["date"] = new AlexaSlot { Value = "2026-W11" }
                    }
                }
            }
        };

        var response = await sut.HandleAsync(request);

        Assert.Contains("konkretes Datum", response.Response.OutputSpeech.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("2026-03-09", true)]
    [InlineData("2026-W11", false)]
    [InlineData("2026", false)]
    [InlineData("", false)]
    public void TryParseAlexaDate_HandlesSupportedFormat(string value, bool expected)
    {
        var ok = AlexaSkillService.TryParseAlexaDate(value, out _);
        Assert.Equal(expected, ok);
    }

    [Fact]
    public void TryValidateRequest_InvalidSkillId_ReturnsFalse()
    {
        var sut = CreateService(new FakeMealPlanDayService(), new AlexaSecurityOptions
        {
            SkillId = "amzn1.ask.skill.allowed",
            ValidateTimestamp = false
        });
        var request = new AlexaRequestEnvelope
        {
            Session = new AlexaSession { Application = new AlexaApplication { ApplicationId = "amzn1.ask.skill.other" } },
            Request = new AlexaRequest { Type = "LaunchRequest" }
        };

        var valid = sut.TryValidateRequest(request, out _);
        Assert.False(valid);
    }

    [Fact]
    public void TryValidateRequest_ExpiredTimestamp_ReturnsFalse()
    {
        var sut = CreateService(new FakeMealPlanDayService(), new AlexaSecurityOptions
        {
            SkillId = "amzn1.ask.skill.allowed",
            ValidateTimestamp = true,
            MaxRequestAgeSeconds = 60
        });
        var request = new AlexaRequestEnvelope
        {
            Session = new AlexaSession { Application = new AlexaApplication { ApplicationId = "amzn1.ask.skill.allowed" } },
            Request = new AlexaRequest
            {
                Type = "LaunchRequest",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O")
            }
        };

        var valid = sut.TryValidateRequest(request, out _);
        Assert.False(valid);
    }

    private static AlexaSkillService CreateService(FakeMealPlanDayService? fake = null, AlexaSecurityOptions? security = null)
    {
        return new AlexaSkillService(
            fake ?? new FakeMealPlanDayService(),
            Options.Create(security ?? new AlexaSecurityOptions
            {
                SkillId = "amzn1.ask.skill.test",
                ValidateTimestamp = true,
                MaxRequestAgeSeconds = 300
            }));
    }

    private sealed class FakeMealPlanDayService : IMealPlanDayService
    {
        public DateOnly LastRequestedDate { get; private set; }

        public Task<MealPlanDayResult> GetDayAsync(DateOnly datum, CancellationToken cancellationToken = default)
        {
            LastRequestedDate = datum;
            return Task.FromResult(new MealPlanDayResult
            {
                Datum = datum,
                Wochentag = "Testtag",
                RezeptId = 1,
                RezeptName = "Testgericht",
                Kategorie = Kategorie.Abendessen,
                Zubereitungszeit = 20,
                SpeechText = $"FAKE {datum:yyyy-MM-dd}"
            });
        }
    }
}
