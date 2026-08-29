using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteDesk.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Number",
                table: "Quotes",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<decimal>(
                name: "Freight",
                table: "Quotes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RequiredBy",
                table: "Quotes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo",
                table: "Quotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidUntil",
                table: "Quotes",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateOnly>(
                name: "DeliveryDate",
                table: "QuoteLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DispatchDate",
                table: "QuoteLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresOverride",
                table: "QuoteLines",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Freight",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RequiredBy",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ShipTo",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "DeliveryDate",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "DispatchDate",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "RequiresOverride",
                table: "QuoteLines");

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                table: "Quotes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);
        }
    }
}
