using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    stripe_customer_id = table.Column<string>(type: "text", nullable: true),
                    stripe_subscription_id = table.Column<string>(type: "text", nullable: true),
                    stripe_checkout_session_id = table.Column<string>(type: "text", nullable: true),
                    payment_reference = table.Column<string>(type: "text", nullable: true),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_subscriptions_users_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_subscriptions_customer_user_id",
                table: "customer_subscriptions",
                column: "customer_user_id",
                unique: true,
                filter: "\"status\" IN ('pending', 'active', 'past_due', 'unpaid', 'incomplete', 'trialing', 'paused')");

            migrationBuilder.CreateIndex(
                name: "ix_customer_subscriptions_customer_user_id_created_at",
                table: "customer_subscriptions",
                columns: new[] { "customer_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_subscriptions_stripe_checkout_session_id",
                table: "customer_subscriptions",
                column: "stripe_checkout_session_id",
                unique: true,
                filter: "\"stripe_checkout_session_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_customer_subscriptions_stripe_subscription_id",
                table: "customer_subscriptions",
                column: "stripe_subscription_id",
                unique: true,
                filter: "\"stripe_subscription_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_subscriptions");
        }
    }
}
