using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuzdan360Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioProFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencySymbol",
                table: "UserDebts",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "InitialAmount",
                table: "UserDebts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestRate",
                table: "UserDebts",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RemainingInstallments",
                table: "UserDebts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalInstallments",
                table: "UserDebts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AssetCategory",
                table: "UserAssets",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "AverageCost",
                table: "UserAssets",
                type: "decimal(18,8)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseDate",
                table: "UserAssets",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Symbol",
                table: "UserAssets",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencySymbol",
                table: "UserDebts");

            migrationBuilder.DropColumn(
                name: "InitialAmount",
                table: "UserDebts");

            migrationBuilder.DropColumn(
                name: "InterestRate",
                table: "UserDebts");

            migrationBuilder.DropColumn(
                name: "RemainingInstallments",
                table: "UserDebts");

            migrationBuilder.DropColumn(
                name: "TotalInstallments",
                table: "UserDebts");

            migrationBuilder.DropColumn(
                name: "AssetCategory",
                table: "UserAssets");

            migrationBuilder.DropColumn(
                name: "AverageCost",
                table: "UserAssets");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "UserAssets");

            migrationBuilder.DropColumn(
                name: "Symbol",
                table: "UserAssets");
        }
    }
}
