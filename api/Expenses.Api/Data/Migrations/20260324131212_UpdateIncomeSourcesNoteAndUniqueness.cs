using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Expenses.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIncomeSourcesNoteAndUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "IncomeSources",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeSources_UserId",
                table: "IncomeSources",
                column: "UserId");

            migrationBuilder.DropIndex(
                name: "IX_IncomeSources_UserId_Name",
                table: "IncomeSources");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IncomeSources_UserId",
                table: "IncomeSources");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "IncomeSources");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeSources_UserId_Name",
                table: "IncomeSources",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }
    }
}
