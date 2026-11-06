using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GroupBuyRequestForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "area",
                table: "group_buy_requests",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "closes_on",
                table: "group_buy_requests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "opens_on",
                table: "group_buy_requests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "shops_wanted",
                table: "group_buy_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "group_buy_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_name",
                table: "group_buy_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_discount_pct",
                table: "group_buy_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "group_buy_request_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_buy_request_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_buy_request_items_group_buy_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "group_buy_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_group_buy_request_items_request_id",
                table: "group_buy_request_items",
                column: "request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_buy_request_items");

            migrationBuilder.DropColumn(
                name: "area",
                table: "group_buy_requests");

            migrationBuilder.DropColumn(
                name: "closes_on",
                table: "group_buy_requests");

            migrationBuilder.DropColumn(
                name: "opens_on",
                table: "group_buy_requests");

            migrationBuilder.DropColumn(
                name: "shops_wanted",
                table: "group_buy_requests");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "group_buy_requests");

            migrationBuilder.DropColumn(
                name: "supplier_name",
                table: "group_buy_requests");

            migrationBuilder.DropColumn(
                name: "target_discount_pct",
                table: "group_buy_requests");
        }
    }
}
