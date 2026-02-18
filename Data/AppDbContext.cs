using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Models;

namespace KuechenRezepte.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Rezept> Rezepte { get; set; }
    public DbSet<Zutat> Zutaten { get; set; }
    public DbSet<RezeptZutat> RezeptZutaten { get; set; }
    public DbSet<Mahlzeit> Mahlzeiten { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Rezept>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Kategorie).HasConversion<string>();
        });

        modelBuilder.Entity<Zutat>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<RezeptZutat>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Rezept)
                .WithMany(r => r.RezeptZutaten)
                .HasForeignKey(e => e.RezeptId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Zutat)
                .WithMany(z => z.RezeptZutaten)
                .HasForeignKey(e => e.ZutatId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Mahlzeit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Datum).IsUnique();
            entity.Property(e => e.Datum).HasColumnType("date");
            entity.HasOne(e => e.Rezept)
                .WithMany()
                .HasForeignKey(e => e.RezeptId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
