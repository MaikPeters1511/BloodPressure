using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanArchitecture.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdDateTimeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BloodPressure_UserId",
                table: "BloodPressure");

            migrationBuilder.CreateIndex(
                name: "IX_BloodPressure_UserId_DateTime",
                table: "BloodPressure",
                columns: new[] { "UserId", "DateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BloodPressure_UserId_DateTime",
                table: "BloodPressure");

            migrationBuilder.CreateIndex(
                name: "IX_BloodPressure_UserId",
                table: "BloodPressure",
                column: "UserId");
        }
    }
}
