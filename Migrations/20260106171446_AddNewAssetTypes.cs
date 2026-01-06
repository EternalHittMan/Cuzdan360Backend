using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Cuzdan360Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddNewAssetTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AssetTypes",
                columns: new[] { "AssetTypeId", "Code", "Name" },
                values: new object[,]
                {
                    { 6, "STK", "Hisse Senedi" },
                    { 7, "FON", "Yatırım Fonu" },
                    { 8, "EMT", "Emtia" },
                    { 9, "BOND", "Tahvil/Bonolar" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AssetTypes",
                keyColumn: "AssetTypeId",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "AssetTypes",
                keyColumn: "AssetTypeId",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "AssetTypes",
                keyColumn: "AssetTypeId",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "AssetTypes",
                keyColumn: "AssetTypeId",
                keyValue: 9);
        }
    }
}
