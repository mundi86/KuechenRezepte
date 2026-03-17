using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using KuechenRezepte.Data;
using KuechenRezepte.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

// Ensure the data directory exists before EF Core tries to open the SQLite file.
var dataSource = connectionString
    .Split(';', StringSplitOptions.RemoveEmptyEntries)
    .Select(p => p.Trim())
    .FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
    ?.Substring("Data Source=".Length);
if (!string.IsNullOrEmpty(dataSource))
{
    var dir = Path.GetDirectoryName(dataSource);
    if (!string.IsNullOrEmpty(dir))
    {
        Directory.CreateDirectory(dir);
    }
}

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.Configure<ApiSecurityOptions>(builder.Configuration.GetSection("Security:Api"));
builder.Services.Configure<AlexaSecurityOptions>(builder.Configuration.GetSection("Security:Alexa"));
builder.Services.Configure<ShoppingListOptions>(builder.Configuration.GetSection("ShoppingList"));
builder.Services.PostConfigure<ShoppingListOptions>(options =>
{
    var defaults = ShoppingListOptions.CreateDefault();

    if (options.ExcludedIngredientTokens.Count == 0)
    {
        options.ExcludedIngredientTokens = defaults.ExcludedIngredientTokens;
    }

    if (options.IngredientAliases.Count == 0)
    {
        options.IngredientAliases = defaults.IngredientAliases;
    }
});
builder.Services.PostConfigure<ApiSecurityOptions>(options =>
{
    var env = Environment.GetEnvironmentVariable("KUECHENREZEPTE_MEALPLAN_API_KEY");
    if (!string.IsNullOrWhiteSpace(env))
    {
        options.MealPlanApiKey = env;
    }
});
builder.Services.PostConfigure<AlexaSecurityOptions>(options =>
{
    var envSkill = Environment.GetEnvironmentVariable("KUECHENREZEPTE_ALEXA_SKILL_ID");
    if (!string.IsNullOrWhiteSpace(envSkill))
    {
        options.SkillId = envSkill;
    }
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("mealplan", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddFixedWindowLimiter("alexa", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

builder.Services.AddScoped<IImageStorage, LocalImageStorage>();
builder.Services.AddScoped<RezeptImageService>();
builder.Services.AddScoped<ChefkochImporter>();
builder.Services.AddScoped<IngredientService>();
builder.Services.AddScoped<RecipeQueryService>();
builder.Services.AddScoped<RecipeCommandService>();
builder.Services.AddScoped<MealPlanService>();
builder.Services.AddScoped<IMealPlanDayService>(sp => sp.GetRequiredService<MealPlanService>());
builder.Services.AddScoped<AlexaSkillService>();
builder.Services.AddScoped<IAlexaRequestSignatureValidator, AlexaRequestSignatureValidator>();
builder.Services.AddScoped<ApiAuditLogger>();
builder.Services.AddScoped<RecipeJsonImportService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/api/mealplan/today", async (
    HttpContext httpContext,
    MealPlanService mealPlanService,
    IOptions<ApiSecurityOptions> securityOptions,
    ApiAuditLogger auditLogger,
    CancellationToken cancellationToken) =>
{
    var clientIp = ApiSecurityHelper.ResolveClientIp(httpContext.Request);
    if (!ApiSecurityHelper.IsApiKeyAuthorized(httpContext.Request.Headers, securityOptions.Value.MealPlanApiKey))
    {
        auditLogger.LogMealPlanAccess("/api/mealplan/today", clientIp, false, "invalid_api_key");
        return Results.Unauthorized();
    }

    var today = DateOnly.FromDateTime(DateTime.Today);
    var result = await mealPlanService.GetDayAsync(today, cancellationToken);
    auditLogger.LogMealPlanAccess("/api/mealplan/today", clientIp, true, "ok");
    return Results.Ok(result);
})
.RequireRateLimiting("mealplan")
.WithName("GetMealPlanToday");

app.MapGet("/api/mealplan/tomorrow", async (
    HttpContext httpContext,
    MealPlanService mealPlanService,
    IOptions<ApiSecurityOptions> securityOptions,
    ApiAuditLogger auditLogger,
    CancellationToken cancellationToken) =>
{
    var clientIp = ApiSecurityHelper.ResolveClientIp(httpContext.Request);
    if (!ApiSecurityHelper.IsApiKeyAuthorized(httpContext.Request.Headers, securityOptions.Value.MealPlanApiKey))
    {
        auditLogger.LogMealPlanAccess("/api/mealplan/tomorrow", clientIp, false, "invalid_api_key");
        return Results.Unauthorized();
    }

    var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
    var result = await mealPlanService.GetDayAsync(tomorrow, cancellationToken);
    auditLogger.LogMealPlanAccess("/api/mealplan/tomorrow", clientIp, true, "ok");
    return Results.Ok(result);
})
.RequireRateLimiting("mealplan")
.WithName("GetMealPlanTomorrow");

app.MapGet("/api/mealplan/day/{date}", async (
    HttpContext httpContext,
    string date,
    MealPlanService mealPlanService,
    IOptions<ApiSecurityOptions> securityOptions,
    ApiAuditLogger auditLogger,
    CancellationToken cancellationToken) =>
{
    var clientIp = ApiSecurityHelper.ResolveClientIp(httpContext.Request);
    if (!ApiSecurityHelper.IsApiKeyAuthorized(httpContext.Request.Headers, securityOptions.Value.MealPlanApiKey))
    {
        auditLogger.LogMealPlanAccess("/api/mealplan/day", clientIp, false, "invalid_api_key");
        return Results.Unauthorized();
    }

    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var targetDate))
    {
        auditLogger.LogMealPlanAccess("/api/mealplan/day", clientIp, false, "invalid_date");
        return Results.BadRequest(new { error = "Ungueltiges Datum. Erwartet: yyyy-MM-dd" });
    }

    var result = await mealPlanService.GetDayAsync(targetDate, cancellationToken);
    auditLogger.LogMealPlanAccess("/api/mealplan/day", clientIp, true, "ok");
    return Results.Ok(result);
})
.RequireRateLimiting("mealplan")
.WithName("GetMealPlanByDate");

app.MapPost("/api/alexa", async (
    HttpContext httpContext,
    AlexaSkillService alexaSkillService,
    IAlexaRequestSignatureValidator signatureValidator,
    IOptions<AlexaSecurityOptions> securityOptions,
    ApiAuditLogger auditLogger,
    CancellationToken cancellationToken) =>
{
    var clientIp = ApiSecurityHelper.ResolveClientIp(httpContext.Request);
    if (!ApiSecurityHelper.IsClientIpAllowed(httpContext.Request, securityOptions.Value.AllowedClientIps))
    {
        auditLogger.LogAlexaAccess(null, null, clientIp, false, "ip_not_allowed");
        return Results.Unauthorized();
    }

    httpContext.Request.EnableBuffering();
    string rawBody;
    using (var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true))
    {
        rawBody = await reader.ReadToEndAsync(cancellationToken);
    }

    httpContext.Request.Body.Position = 0;

    AlexaRequestEnvelope? request;
    try
    {
        request = JsonSerializer.Deserialize<AlexaRequestEnvelope>(rawBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
    catch (JsonException)
    {
        auditLogger.LogAlexaAccess(null, null, clientIp, false, "invalid_json");
        return Results.BadRequest(new { error = "Invalid Alexa payload." });
    }

    if (request == null)
    {
        auditLogger.LogAlexaAccess(null, null, clientIp, false, "missing_payload");
        return Results.BadRequest(new { error = "Missing Alexa payload." });
    }

    if (securityOptions.Value.ValidateSignature)
    {
        var signatureValid = await signatureValidator.ValidateAsync(httpContext.Request, rawBody, cancellationToken);
        if (!signatureValid)
        {
            auditLogger.LogAlexaAccess(request.Request?.Intent?.Name, request.Session?.Application?.ApplicationId, clientIp, false, "invalid_signature");
            return Results.Unauthorized();
        }
    }

    if (!alexaSkillService.TryValidateRequest(request, out var validationError))
    {
        auditLogger.LogAlexaAccess(request.Request?.Intent?.Name, request.Session?.Application?.ApplicationId, clientIp, false, validationError);
        return Results.Unauthorized();
    }

    var response = await alexaSkillService.HandleAsync(request, cancellationToken);
    auditLogger.LogAlexaAccess(request.Request?.Intent?.Name, request.Session?.Application?.ApplicationId, clientIp, true, "ok");
    return Results.Ok(response);
})
.RequireRateLimiting("alexa")
.WithName("AlexaWebhook");

app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await MigrationBootstrapper.EnsureLegacyHistoryAsync(context);
    await context.Database.MigrateAsync();
    await RecipeSeeder.SeedAsync(context);
}

await app.RunAsync();
