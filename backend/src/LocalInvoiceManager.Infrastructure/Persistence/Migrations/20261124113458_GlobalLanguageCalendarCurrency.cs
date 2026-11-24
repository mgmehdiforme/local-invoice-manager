using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalInvoiceManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GlobalLanguageCalendarCurrency : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// The defaults below only apply to a database that already has its settings row (created before phase 8):
        /// it keeps working as before, in Persian with the Jalali calendar. A new database creates the row from the
        /// entity defaults (English, Gregorian, USD).
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Calendar",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Jalali");

            migrationBuilder.AddColumn<string>(
                name: "CurrenciesJson",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "fa");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calendar",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CurrenciesJson",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "AppSettings");
        }
    }
}
