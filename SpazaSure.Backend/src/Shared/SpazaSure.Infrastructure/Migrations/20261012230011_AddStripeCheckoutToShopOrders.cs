using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeCheckoutToShopOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "stripe_checkout_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_checkout_session_id",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_stripe_checkout_id",
                table: "orders",
                column: "stripe_checkout_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_stripe_checkout_session_id",
                table: "orders",
                column: "stripe_checkout_session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_stripe_checkout_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_stripe_checkout_session_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "stripe_checkout_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "stripe_checkout_session_id",
                table: "orders");
        }
    }
}
