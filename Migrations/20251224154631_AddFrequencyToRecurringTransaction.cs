using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuzdan360Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddFrequencyToRecurringTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Frequency",
                table: "RecurringTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "RecurringTransactions");
        }
    }
}
