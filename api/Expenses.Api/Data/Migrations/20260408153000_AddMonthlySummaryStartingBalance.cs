using Expenses.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Expenses.Api.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260408153000_AddMonthlySummaryStartingBalance")]
    public partial class AddMonthlySummaryStartingBalance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StartingBalance",
                table: "MonthlySummaries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartingBalance",
                table: "MonthlySummaries");
        }
    }
}
