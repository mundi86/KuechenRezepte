using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuechenRezepte.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rezepte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Zubereitung = table.Column<string>(type: "TEXT", nullable: true),
                    Portionen = table.Column<int>(type: "INTEGER", nullable: false),
                    Zubereitungszeit = table.Column<int>(type: "INTEGER", nullable: true),
                    Kategorie = table.Column<string>(type: "TEXT", nullable: false),
                    BildPfad = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rezepte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zutaten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zutaten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mahlzeiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    RezeptId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mahlzeiten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mahlzeiten_Rezepte_RezeptId",
                        column: x => x.RezeptId,
                        principalTable: "Rezepte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RezeptZutaten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RezeptId = table.Column<int>(type: "INTEGER", nullable: false),
                    ZutatId = table.Column<int>(type: "INTEGER", nullable: false),
                    Menge = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Einheit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezeptZutaten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezeptZutaten_Rezepte_RezeptId",
                        column: x => x.RezeptId,
                        principalTable: "Rezepte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RezeptZutaten_Zutaten_ZutatId",
                        column: x => x.ZutatId,
                        principalTable: "Zutaten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mahlzeiten_Datum",
                table: "Mahlzeiten",
                column: "Datum",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mahlzeiten_RezeptId",
                table: "Mahlzeiten",
                column: "RezeptId");

            migrationBuilder.CreateIndex(
                name: "IX_RezeptZutaten_RezeptId",
                table: "RezeptZutaten",
                column: "RezeptId");

            migrationBuilder.CreateIndex(
                name: "IX_RezeptZutaten_ZutatId",
                table: "RezeptZutaten",
                column: "ZutatId");

            migrationBuilder.CreateIndex(
                name: "IX_Zutaten_Name",
                table: "Zutaten",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mahlzeiten");

            migrationBuilder.DropTable(
                name: "RezeptZutaten");

            migrationBuilder.DropTable(
                name: "Rezepte");

            migrationBuilder.DropTable(
                name: "Zutaten");
        }
    }
}
