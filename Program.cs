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
builder.Services.AddScoped<IImageStorage, LocalImageStorage>();
builder.Services.AddScoped<RezeptImageService>();
builder.Services.AddScoped<ChefkochImporter>();
builder.Services.AddScoped<IngredientService>();
builder.Services.AddScoped<RecipeQueryService>();
builder.Services.AddScoped<RecipeCommandService>();
builder.Services.AddScoped<MealPlanService>();
builder.Services.AddScoped<IMealPlanDayService>(sp => sp.GetRequiredService<MealPlanService>());
builder.Services.AddScoped<AlexaSkillService>();
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

app.UseAuthorization();

static bool IsMealPlanApiAuthorized(HttpRequest request, ApiSecurityOptions options)
{
    if (string.IsNullOrWhiteSpace(options.MealPlanApiKey))
    {
        return true;
    }

    if (!request.Headers.TryGetValue("X-API-Key", out var provided))
    {
        return false;
    }

    return string.Equals(provided.ToString(), options.MealPlanApiKey, StringComparison.Ordinal);
}

app.MapGet("/api/mealplan/today", async (
    HttpContext httpContext,
    MealPlanService mealPlanService,
    IOptions<ApiSecurityOptions> securityOptions,
    CancellationToken cancellationToken) =>
{
    if (!IsMealPlanApiAuthorized(httpContext.Request, securityOptions.Value))
    {
        return Results.Unauthorized();
    }

    var today = DateOnly.FromDateTime(DateTime.Today);
    var result = await mealPlanService.GetDayAsync(today, cancellationToken);
    return Results.Ok(result);
})
.WithName("GetMealPlanToday");

app.MapGet("/api/mealplan/tomorrow", async (
    HttpContext httpContext,
    MealPlanService mealPlanService,
    IOptions<ApiSecurityOptions> securityOptions,
    CancellationToken cancellationToken) =>
{
    if (!IsMealPlanApiAuthorized(httpContext.Request, securityOptions.Value))
    {
        return Results.Unauthorized();
    }

    var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
    var result = await mealPlanService.GetDayAsync(tomorrow, cancellationToken);
    return Results.Ok(result);
})
.WithName("GetMealPlanTomorrow");

app.MapGet("/api/mealplan/day/{date}", async (
    HttpContext httpContext,
    string date,
    MealPlanService mealPlanService,
    IOptions<ApiSecurityOptions> securityOptions,
    CancellationToken cancellationToken) =>
{
    if (!IsMealPlanApiAuthorized(httpContext.Request, securityOptions.Value))
    {
        return Results.Unauthorized();
    }

    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var targetDate))
    {
        return Results.BadRequest(new { error = "Ungueltiges Datum. Erwartet: yyyy-MM-dd" });
    }

    var result = await mealPlanService.GetDayAsync(targetDate, cancellationToken);
    return Results.Ok(result);
})
.WithName("GetMealPlanByDate");

app.MapPost("/api/alexa", async (
    AlexaRequestEnvelope request,
    AlexaSkillService alexaSkillService,
    CancellationToken cancellationToken) =>
{
    if (!alexaSkillService.TryValidateRequest(request, out _))
    {
        return Results.Unauthorized();
    }

    var response = await alexaSkillService.HandleAsync(request, cancellationToken);
    return Results.Ok(response);
})
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
