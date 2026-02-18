using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Data;
using KuechenRezepte.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddScoped<RezeptImageService>();
builder.Services.AddScoped<ChefkochImporter>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

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

app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.EnsureCreatedAsync();
    await context.Database.ExecuteSqlRawAsync("""
        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Mahlzeiten' AND xtype='U')
        CREATE TABLE Mahlzeiten (
            Id       INT IDENTITY(1,1) PRIMARY KEY,
            Datum    DATE NOT NULL,
            RezeptId INT  NULL,
            CONSTRAINT UQ_Mahlzeiten_Datum UNIQUE (Datum),
            CONSTRAINT FK_Mahlzeiten_Rezept FOREIGN KEY (RezeptId)
                REFERENCES Rezepte(Id) ON DELETE SET NULL
        )
        """);
    await RecipeSeeder.SeedAsync(context);
}

await app.RunAsync();
