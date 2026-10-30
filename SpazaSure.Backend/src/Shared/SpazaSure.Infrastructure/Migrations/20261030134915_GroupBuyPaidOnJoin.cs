using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GroupBuyPaidOnJoin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ending_reminder_sent_at",
                table: "group_buys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "extension_count",
                table: "group_buys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "amount_paid",
                table: "group_buy_participants",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_fee",
                table: "group_buy_participants",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "paid_at",
                table: "group_buy_participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "payment_id",
                table: "group_buy_participants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "group_buy_participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_reference",
                table: "group_buy_participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "group_buy_participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reserved_until",
                table: "group_buy_participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "shop_unit_price",
                table: "group_buy_participant_items",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "ix_group_buy_participants_payment_reference",
                table: "group_buy_participants",
                column: "payment_reference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_group_buy_participants_payment_reference",
                table: "group_buy_participants");

            migrationBuilder.DropColumn(
                name: "ending_reminder_sent_at",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "extension_count",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "amount_paid",
                table: "group_buy_participants");

            migrationBuilder.DropColumn(
                name: "delivery_fee",
                table: "group_buy_participants");

            migrationBuilder.DropColumn(
                name: "paid_at",
                table: "group_buy_participants");

            migrationBuilder.DropColumn(
                name: "payment_id",
                table: "group_buy_participants");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "group_buy_participants");

            migrationBuilder.DropColumn(
                name: "payment_reference",
                table: "group_buy_participants");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "group_buy_participants");

            migrationBuilder.DropColumn(
                name: "reserved_until",
                table: "group_buy_participants");

            migrationBuilder.DropColumn(
                name: "shop_unit_price",
                table: "group_buy_participant_items");
        }
    }
}
