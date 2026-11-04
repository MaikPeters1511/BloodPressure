using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanArchitecture.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BloodPressureId = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notification_BloodPressure_BloodPressureId",
                        column: x => x.BloodPressureId,
                        principalTable: "BloodPressure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Reminder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TimeOfDay = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastFiredOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminder", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_BloodPressureId",
                table: "Notification",
                column: "BloodPressureId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_UserId_ReadAt_CreatedAt",
                table: "Notification",
                columns: new[] { "UserId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminder_UserId_TimeOfDay",
                table: "Reminder",
                columns: new[] { "UserId", "TimeOfDay" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "Reminder");
        }
    }
}
