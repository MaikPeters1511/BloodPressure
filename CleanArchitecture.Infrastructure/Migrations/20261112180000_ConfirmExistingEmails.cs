using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanArchitecture.Infrastructure.Migrations
{
    /// <summary>
    /// Bestehende Konten gelten als bestätigt (ADR 0006). Die Anmeldung verlangt ab jetzt eine bestätigte Adresse; ohne
    /// diese Migration wären alle bisherigen Nutzer ausgesperrt. Nur Daten, kein Schema, daher bleibt der Modell-Snapshot gleich.
    /// </summary>
    [DbContext(typeof(AddPostgresSqlDbContext))]
    [Migration("20261112180000_ConfirmExistingEmails")]
    public partial class ConfirmExistingEmails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"EmailConfirmed\" = TRUE WHERE \"EmailConfirmed\" = FALSE;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Die Bestätigung lässt sich nicht von echten Bestätigungen unterscheiden und wird deshalb nicht zurückgesetzt.
        }
    }
}
