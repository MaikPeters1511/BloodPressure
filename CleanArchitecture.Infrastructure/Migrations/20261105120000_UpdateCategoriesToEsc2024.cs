using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanArchitecture.Infrastructure.Migrations
{
    /// <summary>
    /// Reine Datenmigration (kein Schemawechsel): Gespeicherte Hinweise tragen die Kategorie als Text. Mit der Umstellung
    /// auf ESC 2024 entfallen die Grade 1–3; bestehende Hinweise (nur Grad 3, also ab 180/110 mmHg) werden auf die neue
    /// Kategorie und den neuen Text umgestellt.
    /// </summary>
    public partial class UpdateCategoriesToEsc2024 : Migration
    {
        private const string NewMessage = "Eine Messung liegt im Bereich eines stark erhöhten Blutdrucks (Kategorie Bluthochdruck). Das ist keine Diagnose. Bitte bei Beschwerden oder anhaltend hohen Werten ärztlichen Rat einholen.";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"UPDATE ""Notification""
SET ""Category"" = 'Hypertension', ""Message"" = '{NewMessage}'
WHERE ""Category"" IN ('Grade1', 'Grade2', 'Grade3');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Die frühere Einordnung (Grad 1–3) lässt sich aus der neuen nicht rekonstruieren; Hinweise gab es nur für Grad 3.
            migrationBuilder.Sql(@"UPDATE ""Notification""
SET ""Category"" = 'Grade3', ""Message"" = 'Eine Messung liegt in der Kategorie Hypertonie Grad 3. Das ist keine Diagnose. Bitte bei Beschwerden oder anhaltend hohen Werten ärztlichen Rat einholen.'
WHERE ""Category"" = 'Hypertension';");
        }
    }
}
