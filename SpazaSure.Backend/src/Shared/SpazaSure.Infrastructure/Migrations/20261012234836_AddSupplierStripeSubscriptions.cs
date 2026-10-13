using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierStripeSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cancel_at_period_end",
                table: "supplier_subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "stripe_checkout_session_id",
                table: "supplier_subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_customer_id",
                table: "supplier_subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_subscription_id",
                table: "supplier_subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_subscriptions_stripe_checkout_session_id",
                table: "supplier_subscriptions",
                column: "stripe_checkout_session_id",
                unique: true,
                filter: "\"stripe_checkout_session_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_subscriptions_stripe_subscription_id",
                table: "supplier_subscriptions",
                column: "stripe_subscription_id",
                unique: true,
                filter: "\"stripe_subscription_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplier_subscriptions_stripe_checkout_session_id",
                table: "supplier_subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_supplier_subscriptions_stripe_subscription_id",
                table: "supplier_subscriptions");

            migrationBuilder.DropColumn(
                name: "cancel_at_period_end",
                table: "supplier_subscriptions");

            migrationBuilder.DropColumn(
                name: "stripe_checkout_session_id",
                table: "supplier_subscriptions");

            migrationBuilder.DropColumn(
                name: "stripe_customer_id",
                table: "supplier_subscriptions");

            migrationBuilder.DropColumn(
                name: "stripe_subscription_id",
                table: "supplier_subscriptions");
        }
    }
}
