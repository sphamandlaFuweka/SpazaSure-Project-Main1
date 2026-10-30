using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GroupBuyOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "duration_days",
                table: "group_buys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "supplier_note",
                table: "group_buys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "supplier_responded_at",
                table: "group_buys",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration_days",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "supplier_note",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "supplier_responded_at",
                table: "group_buys");
        }
    }
}
